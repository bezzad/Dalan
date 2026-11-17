using System.Globalization;
using Dalan.Relay.Contracts;
using Dalan.Relay.Data;
using Dalan.Relay.Live;
using Microsoft.EntityFrameworkCore;

namespace Dalan.Relay.Bots;

/// <summary>ADM-12 and REL-38: what the admin door asks of Relay.</summary>
internal sealed class BotAdmin(
    IDbContextFactory<RelayDb> dbFactory, SuspendedBots suspendedBots, IMessageNotifier notifier, TimeProvider time) : IBotAdmin
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

    public void EndUserStreams(long userId) => notifier.EndStreams(userId);

    private static IQueryable<AdminBot> Shape(IQueryable<Bot> bots) => bots.AsNoTracking().Select(b => new AdminBot(
        b.Id, b.Username, b.Name, b.OwnerUserId, b.CreatedAt, b.LastSeenAt, b.SuspendedAt != null));
}
