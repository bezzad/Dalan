using System.Collections.Concurrent;
using Dalan.Relay.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dalan.Relay.Bots;

/// <summary>Whether a bot is online, and when it was last seen (REL-33).</summary>
public sealed record BotStatus(bool Online, DateTimeOffset? LastSeen);

/// <summary>
/// REL-33: the get-updates calls of each bot, in memory. "Last seen" is also saved in relay.bots, at most once per
/// Relay:BotOnlineSeconds, so it survives a restart. With more than one server this becomes shared, like the queue.
/// </summary>
public sealed class BotPresence(IDbContextFactory<RelayDb> dbFactory, IOptionsMonitor<RelayOptions> options, TimeProvider time)
{
    private sealed class State
    {
        public int Polls;
        public DateTimeOffset LastSeen;
        public DateTimeOffset Saved;
    }

    private readonly ConcurrentDictionary<long, State> _bots = new();

    public async Task PollStartsAsync(long botId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var state = _bots.GetOrAdd(botId, _ => new State());
        bool save;
        lock (state)
        {
            state.Polls++;
            state.LastSeen = now;
            save = now - state.Saved >= OnlineTime;
            if (save) state.Saved = now;
        }

        if (!save) return;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Bots.Where(b => b.Id == botId).ExecuteUpdateAsync(set => set.SetProperty(b => b.LastSeenAt, now), cancellationToken);
    }

    public void PollEnds(long botId)
    {
        if (!_bots.TryGetValue(botId, out var state)) return;
        lock (state)
        {
            state.Polls--;
            state.LastSeen = time.GetUtcNow();
        }
    }

    /// <summary>The status from memory, or from the saved time when this server has not seen the bot yet.</summary>
    public BotStatus Of(long botId, DateTimeOffset? saved)
    {
        if (!_bots.TryGetValue(botId, out var state)) return new BotStatus(IsRecent(saved), saved);
        lock (state)
            return new BotStatus(state.Polls > 0 || IsRecent(state.LastSeen), state.LastSeen);
    }

    /// <summary>ADM-19: bots online now: those in memory, and those only <paramref name="recent"/> (saved before a restart) knows.</summary>
    public int CountOnline(IReadOnlyDictionary<long, DateTimeOffset?> recent)
    {
        var known = _bots.Keys.ToHashSet();
        return known.Count(id => Of(id, null).Online) + recent.Count(b => !known.Contains(b.Key) && IsRecent(b.Value));
    }

    public TimeSpan OnlineTime => TimeSpan.FromSeconds(options.CurrentValue.BotOnlineSeconds);

    private bool IsRecent(DateTimeOffset? seen) => seen is { } at && time.GetUtcNow() - at < OnlineTime;
}
