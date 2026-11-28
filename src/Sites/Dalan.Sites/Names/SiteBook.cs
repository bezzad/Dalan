using Dalan.Sites.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Dalan.Sites.Names;

public enum ClaimError { None, BadName, NameTaken, SiteLimit }

/// <summary>The stored sites: who owns which name.</summary>
public sealed class SiteBook(
    IDbContextFactory<SitesDb> dbFactory, IOptionsMonitor<SitesOptions> options, TimeProvider time, NewSiteNotice notice)
{
    /// <summary>
    /// SITE-02 and SITE-03: every name must be good, and free or the caller's. Free names become the caller's sites.
    /// When one name fails, nothing is stored. Returns the names in lower case.
    /// </summary>
    public async Task<(ClaimError Error, string? Name, string[] Names)> ClaimAsync(
        long userId, IReadOnlyList<string?> names, CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;
        var clean = new List<string>();
        foreach (var name in names)
        {
            if (SiteName.Normalize(name, settings) is not { } good) return (ClaimError.BadName, name, []);
            if (!clean.Contains(good)) clean.Add(good);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var found = await db.Sites.Where(s => clean.Contains(s.Name)).ToListAsync(cancellationToken);
        if (found.FirstOrDefault(s => s.OwnerUserId != userId) is { } taken) return (ClaimError.NameTaken, taken.Name, []);

        var fresh = clean.Where(name => found.All(s => s.Name != name)).ToList();
        if (fresh.Count == 0) return (ClaimError.None, null, [.. clean]);

        var owned = await db.Sites.CountAsync(s => s.OwnerUserId == userId, cancellationToken);
        if (owned + fresh.Count > settings.MaxSitesPerBuilder) return (ClaimError.SiteLimit, fresh[0], []);

        db.Sites.AddRange(fresh.Select(name => new Site { OwnerUserId = userId, Name = name, CreatedAt = time.GetUtcNow() }));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Another builder took one of the names a moment ago.
            return (ClaimError.NameTaken, fresh[0], []);
        }

        // SITE-25: only the names made now; a name the caller had already (a reconnect) says nothing.
        await notice.TellAdminsAsync(userId, fresh, settings, cancellationToken);
        return (ClaimError.None, null, [.. clean]);
    }

    public async Task<List<Site>> ListAsync(long userId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Sites.Where(s => s.OwnerUserId == userId).OrderBy(s => s.Name).ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(string name, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Sites.AnyAsync(s => s.Name == name, cancellationToken);
    }

    /// <summary>Deletes a site of this user (any site for an admin). Returns its name, or null when there is no such site.</summary>
    public async Task<string?> DeleteAsync(long userId, bool isAdmin, long siteId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var site = await db.Sites.FirstOrDefaultAsync(s => s.Id == siteId && (isAdmin || s.OwnerUserId == userId), cancellationToken);
        if (site is null) return null;
        db.Sites.Remove(site);
        await db.SaveChangesAsync(cancellationToken);
        return site.Name;
    }
}
