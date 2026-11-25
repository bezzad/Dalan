using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Dalan.Relay.ClientApi;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dalan.Relay.Live;

/// <summary>LIVE-01: the Server-Sent Events stream of one user. It carries the new messages of all that user's chats.</summary>
internal static class EventStream
{
    public static async Task RunAsync(
        HttpContext http,
        IMessageNotifier notifier,
        IOptionsMonitor<RelayOptions> options,
        TimeProvider time,
        IHostApplicationLifetime lifetime)
    {
        http.Response.ContentType = "text/event-stream";
        http.Response.Headers.CacheControl = "no-cache";
        // Tells a buffering proxy to pass each event on at once.
        http.Response.Headers["X-Accel-Buffering"] = "no";

        using var subscription = notifier.Subscribe(ClientApiFilter.UserId(http));

        // LIVE-07: the stream ends with the session, with the server, or when the browser goes away.
        using var sessionEnd = new CancellationTokenSource(SessionEnd(http.User) - time.GetUtcNow() is { Ticks: > 0 } left ? left : TimeSpan.Zero, time);
        using var end = CancellationTokenSource.CreateLinkedTokenSource(
            sessionEnd.Token, lifetime.ApplicationStopping, http.RequestAborted);

        try
        {
            await WriteAsync(http, ": connected\n\n", end.Token);
            while (await NextAsync(http, subscription, options.CurrentValue, time, end.Token))
            {
                while (subscription.Events.TryRead(out var liveEvent))
                    await WriteAsync(http, Format(liveEvent), end.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>LIVE-02, LIVE-09 and LIVE-10: one Server-Sent Event.</summary>
    private static string Format(LiveEvent liveEvent) => liveEvent switch
    {
        MessageChanged changed =>
            $"event: message\ndata: {JsonSerializer.Serialize(new MessageEventJson(changed.BotId, changed.Message), ApiJson.Options)}\n\n",
        CallbackAnswered answer =>
            $"event: answer\ndata: {JsonSerializer.Serialize(new AnswerEventJson(answer.BotId, answer.CallbackId, answer.Text, answer.ShowAlert), ApiJson.Options)}\n\n",
        ChatsReset => "event: reset\ndata: {}\n\n",
        ChatRead read =>
            $"event: read\ndata: {JsonSerializer.Serialize(new ReadEventJson(read.BotId, read.Unread), ApiJson.Options)}\n\n",
        _ => throw new ArgumentOutOfRangeException(nameof(liveEvent)),
    };

    /// <summary>Waits for the next event and sends a heartbeat while there is none (LIVE-05). False when the stream must end.</summary>
    private static async Task<bool> NextAsync(
        HttpContext http, IMessageSubscription subscription, RelayOptions options, TimeProvider time, CancellationToken end)
    {
        while (true)
        {
            using var heartbeat = new CancellationTokenSource(TimeSpan.FromSeconds(options.StreamHeartbeatSeconds), time);
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(end, heartbeat.Token);
            try
            {
                return await subscription.Events.WaitToReadAsync(wait.Token);
            }
            catch (OperationCanceledException) when (!end.IsCancellationRequested)
            {
                await WriteAsync(http, ": ping\n\n", end);
            }
        }
    }

    private static async Task WriteAsync(HttpContext http, string text, CancellationToken cancellationToken)
    {
        await http.Response.WriteAsync(text, cancellationToken);
        await http.Response.Body.FlushAsync(cancellationToken);
    }

    private static DateTimeOffset SessionEnd(ClaimsPrincipal user) =>
        DateTimeOffset.FromUnixTimeSeconds(long.Parse(user.FindFirstValue("exp")!, CultureInfo.InvariantCulture));
}
