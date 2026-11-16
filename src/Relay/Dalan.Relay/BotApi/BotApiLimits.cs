using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace Dalan.Relay.BotApi;

/// <summary>SEC-01 and SEC-02: at most N calls per window for each bot and for each client address.</summary>
internal sealed class BotApiLimits(IOptionsMonitor<RelayOptions> options) : IAsyncDisposable
{
    private readonly PartitionedRateLimiter<string> _perIp = PartitionedRateLimiter.Create<string, string>(
        ip => RateLimitPartition.GetFixedWindowLimiter(ip, _ => Window(options.CurrentValue, o => o.IpRequestLimit)));

    private readonly PartitionedRateLimiter<long> _perBot = PartitionedRateLimiter.Create<long, long>(
        botId => RateLimitPartition.GetFixedWindowLimiter(botId, _ => Window(options.CurrentValue, o => o.BotRequestLimit)));

    /// <summary>Returns null when the call may go on, otherwise how long the caller should wait.</summary>
    public TimeSpan? CheckIp(string ip) => Check(_perIp, ip);

    public TimeSpan? CheckBot(long botId) => Check(_perBot, botId);

    private TimeSpan? Check<TKey>(PartitionedRateLimiter<TKey> limiter, TKey key)
    {
        using var lease = limiter.AttemptAcquire(key);
        if (lease.IsAcquired) return null;

        return lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? retryAfter
            : TimeSpan.FromSeconds(options.CurrentValue.RateLimitWindowSeconds);
    }

    private static FixedWindowRateLimiterOptions Window(RelayOptions options, Func<RelayOptions, int> limit) => new()
    {
        PermitLimit = limit(options),
        Window = TimeSpan.FromSeconds(options.RateLimitWindowSeconds),
        QueueLimit = 0,
    };

    public async ValueTask DisposeAsync()
    {
        await _perIp.DisposeAsync();
        await _perBot.DisposeAsync();
    }
}
