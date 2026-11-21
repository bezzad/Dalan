using System.Security.Cryptography;
using Dalan.Sites.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dalan.Sites.Demos;

public enum DemoError { None, Limit, NotFound, Ended }

/// <summary>The stored demos (SITE-18 to SITE-20).</summary>
public sealed class DemoBook(IDbContextFactory<SitesDb> dbFactory, IOptionsMonitor<SitesOptions> options, TimeProvider time)
{
    private const string IdCharacters = "abcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>SITE-18: a new demo for this user, or the user's own live demo again (a reconnect).</summary>
    public async Task<(DemoError Error, Demo? Demo)> OpenAsync(long userId, string demo, CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;
        var now = time.GetUtcNow();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (demo != "new")
        {
            var own = await db.Demos.FirstOrDefaultAsync(d => d.Id == demo && d.OwnerUserId == userId, cancellationToken);
            return own is null ? (DemoError.NotFound, null) : own.IsLive(now) ? (DemoError.None, own) : (DemoError.Ended, null);
        }

        var live = await db.Demos.CountAsync(d => d.OwnerUserId == userId && d.EndedAt == null && d.ExpiresAt > now, cancellationToken);
        if (live >= settings.MaxDemosPerBuilder) return (DemoError.Limit, null);

        var fresh = new Demo
        {
            Id = RandomNumberGenerator.GetString(IdCharacters, settings.DemoIdLength),
            OwnerUserId = userId,
            CreatedAt = now,
            ExpiresAt = now.AddHours(settings.DemoHours),
        };
        db.Demos.Add(fresh);
        await db.SaveChangesAsync(cancellationToken);
        return (DemoError.None, fresh);
    }

    public async Task<Demo?> FindAsync(string id, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Demos.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    /// <summary>SITE-20: the owner ends the demo. False when the user has no such demo. Ending twice is fine.</summary>
    public async Task<bool> EndAsync(long userId, string id, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var demo = await db.Demos.FirstOrDefaultAsync(d => d.Id == id && d.OwnerUserId == userId, cancellationToken);
        if (demo is null) return false;
        demo.EndedAt ??= time.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
