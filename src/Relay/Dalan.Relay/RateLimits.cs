using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace Dalan.Relay;

/// <summary>
/// SEC-01 and SEC-03: at most N calls per window for each bot and for each user.
/// The limit per client address (SEC-02) is in the host, in front of every service.
/// </summary>
internal sealed class RateLimits(IOptionsMonitor<RelayOptions> options) : IAsyncDisposable
{
    private readonly PartitionedRateLimiter<long> _perBot = PartitionedRateLimiter.Create<long, long>(
        botId => RateLimitPartition.GetFixedWindowLimiter(botId, _ => Window(options.CurrentValue, o => o.BotRequestLimit)));

    private readonly PartitionedRateLimiter<long> _perUser = PartitionedRateLimiter.Create<long, long>(
        userId => RateLimitPartition.GetFixedWindowLimiter(userId, _ => Window(options.CurrentValue, o => o.UserRequestLimit)));

    /// <summary>Returns null when the call may go on, otherwise how long the caller should wait.</summary>
    public TimeSpan? CheckBot(long botId) => Check(_perBot, botId);

    public TimeSpan? CheckUser(long userId) => Check(_perUser, userId);

    private TimeSpan? Check(PartitionedRateLimiter<long> limiter, long key)
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
        await _perBot.DisposeAsync();
        await _perUser.DisposeAsync();
    }
}
