using System.Net;
using System.Text.Json;
using Dalan.Notification.Contracts;
using Dalan.Notification.Data;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dalan.Notification.Push;

/// <summary>NTF-06 to NTF-09: takes pushes from the queue and sends them, a few at once.</summary>
public sealed partial class PushWorker(
    WebPushService service,
    IDbContextFactory<NotificationDb> dbFactory,
    IHttpClientFactory httpFactory,
    IOptionsMonitor<NotificationOptions> options,
    ILogger<PushWorker> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => service.PublicKey is null
        ? Task.CompletedTask
        : Task.WhenAll(Enumerable.Range(0, options.CurrentValue.Push.Workers).Select(_ => RunAsync(stoppingToken)));

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        // One VAPID signer per worker: its key object is not shared between threads.
        var push = options.CurrentValue.Push;
        using var vapid = new VapidAuthentication(push.PublicKey.Trim(), push.PrivateKey.Trim()) { Subject = push.Subject };
        var client = new PushServiceClient(httpFactory.CreateClient(nameof(PushWorker)))
        {
            DefaultAuthentication = vapid,
            AutoRetryAfter = false,
        };

        try
        {
            await foreach (var (userId, note) in service.Queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await SendAsync(client, userId, note, stoppingToken);
                }
#pragma warning disable CA1031 // One failed push (database down) must not stop the worker. The message arrives by sync.
                catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
#pragma warning restore CA1031
                {
                    LogPushFailed(exception.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    /// <summary>NTF-06: one encrypted POST to each device of the user.</summary>
    private async Task SendAsync(PushServiceClient client, long userId, PushNote note, CancellationToken cancellationToken)
    {
        List<Subscription> devices;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
            devices = await db.Subscriptions.AsNoTracking().Where(s => s.UserId == userId).ToListAsync(cancellationToken);
        if (devices.Count == 0) return;

        var push = options.CurrentValue.Push;
        var payload = JsonSerializer.Serialize(note with { Body = Cut(note.Body, push.MaxBodyLength) }, Json);
        foreach (var device in devices)
        {
            var subscription = new PushSubscription { Endpoint = device.Endpoint };
            subscription.SetKey(PushEncryptionKeyName.P256DH, device.P256dh);
            subscription.SetKey(PushEncryptionKeyName.Auth, device.Auth);
            var message = new PushMessage(payload) { Topic = note.Tag, Urgency = PushMessageUrgency.High, TimeToLive = push.TtlSeconds };
            var host = new Uri(device.Endpoint).Host;
            try
            {
                await client.RequestPushMessageDeliveryAsync(subscription, message, cancellationToken);
            }
            catch (PushServiceClientException exception) when (exception.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                // NTF-08: the browser dropped this subscription.
                await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
                await db.Subscriptions.Where(s => s.Id == device.Id).ExecuteDeleteAsync(cancellationToken);
                LogGone(host);
            }
            catch (PushServiceClientException exception)
            {
                // NTF-09: only the host and the status, never the keys or the text.
                LogRefused(host, (int)exception.StatusCode);
            }
            catch (Exception exception) when (exception is HttpRequestException
                || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested))
            {
                LogUnreachable(host, exception.GetType().Name);
            }
        }
    }

    /// <summary>NTF-06: at most <paramref name="max"/> characters, never half a character.</summary>
    public static string Cut(string text, int max)
    {
        if (text.Length <= max) return text;
        var end = max - 1;
        if (end > 0 && char.IsHighSurrogate(text[end - 1])) end--;
        return string.Concat(text.AsSpan(0, Math.Max(end, 0)), "…");
    }

    [LoggerMessage(LogLevel.Information, "Push subscription at {Host} is gone; it was deleted.")]
    private partial void LogGone(string host);

    [LoggerMessage(LogLevel.Warning, "Push failed: {Host} answered {Status}. The subscription stays.")]
    private partial void LogRefused(string host, int status);

    [LoggerMessage(LogLevel.Warning, "Push failed: {Host} could not be reached ({Error}). The subscription stays.")]
    private partial void LogUnreachable(string host, string error);

    [LoggerMessage(LogLevel.Error, "A push failed ({Error}); the message arrives by sync.")]
    private partial void LogPushFailed(string error);
}
