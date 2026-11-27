using System.Globalization;
using Dalan.Relay.Contracts;
using Dalan.Relay.Data;
using Dalan.Relay.Live;
using Dalan.Relay.Showcase;
using Microsoft.EntityFrameworkCore;

namespace Dalan.Relay.Bots;

/// <summary>ADM-12 and REL-38: what the admin door asks of Relay.</summary>
internal sealed class BotAdmin(
    IDbContextFactory<RelayDb> dbFactory, SuspendedBots suspendedBots, IMessageNotifier notifier, BotPresence presence, TimeProvider time)
    : IBotAdmin
{
    public async Task<IReadOnlyList<AdminBot>> SearchAsync(
        string query, long? before, int limit, CancellationToken cancellationToken)
    {
        var text = query.Trim().TrimStart('@');
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var bots = db.Bots.Where(b => before == null || b.Id < before);
        if (text.Length > 0)
        {
            var id = long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : -1;
#pragma warning disable CA1308 // Bot usernames are stored in lower case (REL-36).
            var lower = text.ToLowerInvariant();
#pragma warning restore CA1308
#pragma warning disable CA1304, CA1311, CA1862 // Runs in PostgreSQL as lower(username), which the unique index covers.
            bots = bots.Where(b => b.Id == id || b.OwnerUserId == id || b.Username.ToLower() == lower);
#pragma warning restore CA1304, CA1311, CA1862
        }

        return await Shape(db, bots.OrderByDescending(b => b.Id).Take(limit)).ToListAsync(cancellationToken);
    }

    public async Task<AdminBot?> FindAsync(long botId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await Shape(db, db.Bots.Where(b => b.Id == botId)).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminBot>> OwnedByAsync(long ownerUserId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await Shape(db, db.Bots.Where(b => b.OwnerUserId == ownerUserId && b.DeletedAt == null).OrderByDescending(b => b.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<long, int>> CountByOwnerAsync(
        IReadOnlyCollection<long> ownerUserIds, CancellationToken cancellationToken)
    {
        if (ownerUserIds.Count == 0) return new Dictionary<long, int>();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Bots.AsNoTracking()
            .Where(b => ownerUserIds.Contains(b.OwnerUserId) && b.DeletedAt == null)
            .GroupBy(b => b.OwnerUserId)
            .Select(g => new { Owner = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Owner, g => g.Count, cancellationToken);
    }

    public async Task<AdminBot?> SetSuspendedAsync(long botId, bool suspended, CancellationToken cancellationToken)
    {
        var at = suspended ? time.GetUtcNow() : (DateTimeOffset?)null;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var changed = await db.Bots.Where(b => b.Id == botId)
            .ExecuteUpdateAsync(set => set.SetProperty(b => b.SuspendedAt, b => suspended ? b.SuspendedAt ?? at : null), cancellationToken);
        if (changed == 0) return null;

        suspendedBots.Set(botId, suspended);
        return await Shape(db, db.Bots.Where(b => b.Id == botId)).FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShowcaseRequest>> ShowcaseRequestsAsync(int limit, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Showcase.AsNoTracking()
            .Where(e => e.State == ShowcaseState.Pending)
            .OrderBy(e => e.UpdatedAt)
            .Take(limit)
            .Join(db.Bots, e => e.BotId, b => b.Id, (e, b) => new ShowcaseRequest(
                b.Id, b.Username, b.Name, b.OwnerUserId, b.SuspendedAt != null, e.Description, e.Category, b.IconType != null, e.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ReviewShowcaseAsync(long botId, bool approve, CancellationToken cancellationToken)
    {
        var state = approve ? ShowcaseState.Listed : ShowcaseState.Refused;
        var at = time.GetUtcNow();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Showcase.Where(e => e.BotId == botId)
            .ExecuteUpdateAsync(set => set.SetProperty(e => e.State, state).SetProperty(e => e.ReviewedAt, at), cancellationToken) > 0;
    }

    public async Task<BotCounts> CountAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var since = time.GetUtcNow() - presence.OnlineTime;
        var recent = await db.Bots.AsNoTracking().Where(b => b.LastSeenAt >= since)
            .Select(b => new { b.Id, b.LastSeenAt }).ToDictionaryAsync(b => b.Id, b => b.LastSeenAt, cancellationToken);
        return new BotCounts(
            await db.Bots.CountAsync(cancellationToken),
            presence.CountOnline(recent),
            await db.Showcase.CountAsync(e => e.State == ShowcaseState.Pending, cancellationToken));
    }

    public void EndUserStreams(long userId) => notifier.EndStreams(userId);

    /// <summary>ADM-12: "users" counts every chat of the bot, whatever its state: each is a person who started it.</summary>
    private static IQueryable<AdminBot> Shape(RelayDb db, IQueryable<Bot> bots) => bots.AsNoTracking().Select(b => new AdminBot(
        b.Id, b.Username, b.Name, b.OwnerUserId, b.CreatedAt, b.LastSeenAt, b.SuspendedAt != null,
        db.Memberships.Count(m => m.BotId == b.Id)));
}
