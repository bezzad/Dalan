using System.Collections.Concurrent;
using Dalan.Sites.Contracts;
using Dalan.Sites.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dalan.Sites.Login;

/// <summary>
/// SITE-26: which sites ask for Dalan's login on the proxy. Kept in the database and in memory, so the proxy asks no database
/// on each request. AUTH-40: Identity reads a site's owner, address and this switch through <see cref="ISiteDirectory"/>.
/// </summary>
internal sealed class SiteLogins(IDbContextFactory<SitesDb> dbFactory, IOptionsMonitor<SitesOptions> options, TimeProvider time) : ISiteDirectory
{
    private readonly ConcurrentDictionary<string, bool> _on = new(StringComparer.Ordinal);

    public bool IsOn(string site) => _on.ContainsKey(site);

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var names = await db.Database.SqlQuery<string>(
            $"SELECT s.name AS \"Value\" FROM sites.sites s JOIN sites.proxy_logins p ON p.site_id = s.id").ToListAsync(cancellationToken);
        _on.Clear();
        foreach (var name in names) _on[name] = true;
    }

    /// <summary>Turns the login on or off for a site of the caller's. False when the caller has no such site.</summary>
    public async Task<bool> SetAsync(long userId, long siteId, bool on, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(s => s.Id == siteId && s.OwnerUserId == userId, cancellationToken);
        if (site is null) return false;
        if (on)
        {
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO sites.proxy_logins (site_id, created_at) VALUES ({siteId}, {time.GetUtcNow()}) ON CONFLICT DO NOTHING",
                cancellationToken);
            _on[site.Name] = true;
        }
        else
        {
            await db.Database.ExecuteSqlAsync($"DELETE FROM sites.proxy_logins WHERE site_id = {siteId}", cancellationToken);
            _on.TryRemove(site.Name, out _);
        }

        return true;
    }

    /// <summary>SITE-26: "dalan http --login" turns the login on for these sites of the caller's. Returns the names it was off for.</summary>
    public async Task<List<string>> TurnOnAsync(long userId, string[] names, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var turned = await db.Database.SqlQuery<string>($"""
            INSERT INTO sites.proxy_logins (site_id, created_at)
            SELECT s.id, {time.GetUtcNow()} FROM sites.sites s WHERE s.owner_user_id = {userId} AND s.name = ANY({names})
            ON CONFLICT DO NOTHING
            RETURNING (SELECT name FROM sites.sites WHERE id = site_id) AS "Value"
            """).ToListAsync(cancellationToken);
        foreach (var name in turned) _on[name] = true;
        return turned;
    }

    /// <summary>The site is gone; its row went with it in the database.</summary>
    public void Forget(string site) => _on.TryRemove(site, out _);

    public async Task<SiteLoginInfo?> FindAsync(string name, CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;
        if (settings.Domain.Length == 0) return null;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var owner = await db.Sites.Where(s => s.Name == name).Select(s => (long?)s.OwnerUserId).FirstOrDefaultAsync(cancellationToken);
        return owner is null ? null : new SiteLoginInfo(owner.Value, settings.UrlOf(name), IsOn(name));
    }
}
