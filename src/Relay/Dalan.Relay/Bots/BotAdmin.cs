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
    public async Task<IReadOnlyList<AdminBot>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        var text = query.Trim().TrimStart('@');
        if (text.Length == 0) return [];
        var id = long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : -1;
#pragma warning disable CA1308 // Bot usernames are stored in lower case (REL-36).
        var lower = text.ToLowerInvariant();
#pragma warning restore CA1308

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await Shape(db.Bots
#pragma warning disable CA1304, CA1311, CA1862 // Runs in PostgreSQL as lower(username), which the unique index covers.
                .Where(b => b.Id == id || b.OwnerUserId == id || b.Username.ToLower() == lower)
#pragma warning restore CA1304, CA1311, CA1862
                .OrderBy(b => b.Id)
                .Take(limit))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminBot?> SetSuspendedAsync(long botId, bool suspended, CancellationToken cancellationToken)
    {
        var at = suspended ? time.GetUtcNow() : (DateTimeOffset?)null;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var changed = await db.Bots.Where(b => b.Id == botId)
            .ExecuteUpdateAsync(set => set.SetProperty(b => b.SuspendedAt, b => suspended ? b.SuspendedAt ?? at : null), cancellationToken);
        if (changed == 0) return null;

        suspendedBots.Set(botId, suspended);
        return await Shape(db.Bots.Where(b => b.Id == botId)).FirstAsync(cancellationToken);
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

    private static IQueryable<AdminBot> Shape(IQueryable<Bot> bots) => bots.AsNoTracking().Select(b => new AdminBot(
        b.Id, b.Username, b.Name, b.OwnerUserId, b.CreatedAt, b.LastSeenAt, b.SuspendedAt != null));
}
