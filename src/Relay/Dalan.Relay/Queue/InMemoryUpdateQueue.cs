using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Dalan.Relay.Queue;

public sealed class InMemoryUpdateQueue(IOptionsMonitor<RelayOptions> options, TimeProvider time) : IUpdateQueue
{
    private readonly ConcurrentDictionary<long, BotQueue> _bots = new();

    public IReadOnlyList<long> Enqueue(long botId, QueuedUpdate update) =>
        Of(botId).Enqueue(update, options.CurrentValue.LatestCommandWins);

    public Batch Take(long botId, long offset, int limit) =>
        Of(botId).Take(offset, limit, time.GetUtcNow(), options.CurrentValue.UpdateExpiry);

    public IReadOnlyList<long> RemoveExpired()
    {
        var now = time.GetUtcNow();
        var expiry = options.CurrentValue.UpdateExpiry;
        return _bots.Values.SelectMany(bot => bot.RemoveExpired(now, expiry)).ToList();
    }

    public CancellationToken BeginPoll(long botId) => Of(botId).BeginPoll();

    private BotQueue Of(long botId) => _bots.GetOrAdd(botId, _ => new BotQueue());
}
