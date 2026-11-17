using Dalan.Relay.Data;
using Microsoft.EntityFrameworkCore;

namespace Dalan.Relay.Bots;

/// <summary>
/// REL-38: the ids of suspended bots, in memory, so a user's message needs no extra query. Loaded at start.
/// With more than one server this must become shared, like the queue (docs/ARCHITECTURE.md section 8).
/// </summary>
public sealed class SuspendedBots(IDbContextFactory<RelayDb> dbFactory)
{
    private readonly Lock _lock = new();
    private HashSet<long> _ids = [];

    public bool Contains(long botId)
    {
        lock (_lock) return _ids.Contains(botId);
    }

    public void Set(long botId, bool suspended)
    {
        lock (_lock)
        {
            if (suspended) _ids.Add(botId);
            else _ids.Remove(botId);
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var ids = await db.Bots.Where(b => b.SuspendedAt != null).Select(b => b.Id).ToListAsync(cancellationToken);
        lock (_lock) _ids = [.. ids];
    }
}
