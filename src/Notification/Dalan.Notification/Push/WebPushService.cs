using System.Buffers.Text;
using System.Threading.Channels;
using Dalan.Notification.Contracts;
using Dalan.Notification.Data;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dalan.Notification.Push;

/// <summary>One push in the queue. <see cref="Delivered"/>, when set, gets the number of devices that took it (NTF-13).</summary>
internal sealed record PushJob(long UserId, PushNote Note, TaskCompletionSource<int>? Delivered);

/// <summary>NTF-03 to NTF-07: the subscriptions, and the queue that <see cref="PushWorker"/> sends from.</summary>
public sealed partial class WebPushService : IWebPush
{
    private readonly IDbContextFactory<NotificationDb> _dbFactory;
    private readonly IOptionsMonitor<NotificationOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<WebPushService> _logger;
    private readonly Channel<PushJob> _queue;

    public WebPushService(
        IDbContextFactory<NotificationDb> dbFactory,
        IOptionsMonitor<NotificationOptions> options,
        TimeProvider time,
        ILogger<WebPushService> logger)
    {
        _dbFactory = dbFactory;
        _options = options;
        _time = time;
        _logger = logger;
        var push = options.CurrentValue.Push;
        PublicKey = CheckKeys(push);
        _queue = Channel.CreateBounded<PushJob>(push.QueueSize);
    }

    public string? PublicKey { get; }

    internal ChannelReader<PushJob> Queue => _queue.Reader;

    public async Task<SubscribeResult> SubscribeAsync(long userId, PushDevice device, CancellationToken cancellationToken)
    {
        if (PublicKey is null) return SubscribeResult.PushOff;

        var push = _options.CurrentValue.Push;
        if (!IsAllowed(device.Endpoint, push) || !IsKey(device.P256dh, 65) || !IsKey(device.Auth, 16)) return SubscribeResult.Refused;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var now = _time.GetUtcNow();
        // NTF-03: an endpoint has one owner; the newest one wins.
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO notification.push_subscriptions (user_id, endpoint, p256dh, auth, created_at)
            VALUES ({userId}, {device.Endpoint}, {device.P256dh}, {device.Auth}, {now})
            ON CONFLICT (endpoint) DO UPDATE
            SET user_id = excluded.user_id, p256dh = excluded.p256dh, auth = excluded.auth, created_at = excluded.created_at
            """, cancellationToken);

        var oldest = await db.Subscriptions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id)
            .Skip(push.MaxSubscriptionsPerUser)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);
        if (oldest.Count > 0) await db.Subscriptions.Where(s => oldest.Contains(s.Id)).ExecuteDeleteAsync(cancellationToken);
        return SubscribeResult.Saved;
    }

    public async Task UnsubscribeAsync(long userId, string endpoint, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Subscriptions.Where(s => s.UserId == userId && s.Endpoint == endpoint).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task MoveAsync(long fromUserId, long intoUserId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Subscriptions.Where(s => s.UserId == fromUserId)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.UserId, intoUserId), cancellationToken);

        var oldest = await db.Subscriptions
            .Where(s => s.UserId == intoUserId)
            .OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id)
            .Skip(_options.CurrentValue.Push.MaxSubscriptionsPerUser)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);
        if (oldest.Count > 0) await db.Subscriptions.Where(s => oldest.Contains(s.Id)).ExecuteDeleteAsync(cancellationToken);
    }

    public void Send(long userId, PushNote note)
    {
        if (PublicKey is null) return;
        if (!_queue.Writer.TryWrite(new PushJob(userId, note, null))) LogDropped();
    }

    /// <summary>
    /// NTF-13: like <see cref="Send"/>, and the task tells how many devices took the push once it was tried:
    /// 0 when push is off, the queue is full, the user has no device, or every device failed.
    /// </summary>
    internal Task<int> DeliverAsync(long userId, PushNote note)
    {
        if (PublicKey is null) return Task.FromResult(0);
        var delivered = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (_queue.Writer.TryWrite(new PushJob(userId, note, delivered))) return delivered.Task;
        LogDropped();
        return Task.FromResult(0);
    }

    /// <summary>NTF-05: null when push is off; a wrong pair stops the start.</summary>
    private static string? CheckKeys(PushOptions push)
    {
        var publicKey = push.PublicKey.Trim();
        var privateKey = push.PrivateKey.Trim();
        if (publicKey.Length == 0 && privateKey.Length == 0) return null;

        const string Fix = "Set Notification:Push:PublicKey and Notification:Push:PrivateKey to a VAPID key pair in base64url " +
            "(make one with: node scripts/vapid-keys.mjs), and Notification:Push:Subject to an https address or mailto:, " +
            "or leave both keys empty to turn push off.";
        if (!IsKey(publicKey, 65) || !IsKey(privateKey, 32)) throw new InvalidOperationException("Notification:Push keys are not valid. " + Fix);
        try
        {
            using var vapid = new VapidAuthentication(publicKey, privateKey) { Subject = push.Subject };
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException("Notification:Push settings are not valid. " + Fix, exception);
        }

        return publicKey;
    }

    /// <summary>NTF-04: https on the standard port, on a known push service or a subdomain of one.</summary>
    private static bool IsAllowed(string? endpoint, PushOptions push)
    {
        if (endpoint is null || endpoint.Length > push.MaxEndpointLength
            || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || uri.UserInfo.Length > 0)
        {
            return false;
        }

        var host = uri.IdnHost;
        return push.AllowedHosts.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(allowed =>
            host.Equals(allowed, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + allowed, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsKey(string? text, int bytes)
    {
        if (string.IsNullOrEmpty(text) || text.Length > 200) return false;
        try
        {
            return Base64Url.DecodeFromChars(text).Length == bytes;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    [LoggerMessage(LogLevel.Warning, "The push queue is full; a push was dropped. The message itself is safe and arrives by sync.")]
    private partial void LogDropped();
}
