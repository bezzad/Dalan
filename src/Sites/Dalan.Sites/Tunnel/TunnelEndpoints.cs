using System.Buffers;
using System.Globalization;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Channels;
using Dalan.Admin.Contracts;
using Dalan.Identity.Contracts;
using Dalan.Sites.Demos;
using Dalan.Sites.Login;
using Dalan.Sites.Names;
using Dalan.Sites.Proxy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dalan.Sites.Tunnel;

/// <summary>SITE-03 to SITE-07: the doors of the agent. A session or an access key of a builder may call.</summary>
internal static class TunnelEndpoints
{
    private const int MaxRequestsPerPoll = 100;

    public static void MapTunnel(this IEndpointRouteBuilder endpoints)
    {
        var tunnel = endpoints.MapGroup("/api/tunnel").RequireAuthorization(AccessKeys.SessionOrKeyPolicy)
            .AddEndpointFilter<BuilderOnly>();
        tunnel.MapPost("/sessions", OpenAsync);
        tunnel.MapDelete("/sessions/{session}", EndSessionAsync);
        tunnel.MapDelete("/demos/{id}", EndDemoAsync);
        tunnel.MapGet("/sessions/{session}/ws", WebSocketAsync);
        tunnel.MapGet("/sessions/{session}/requests", PollAsync);
        tunnel.MapPost("/sessions/{session}/responses", AnswerAsync);
    }

    /// <summary>The user of the session or key. "sub" is the standard JWT claim; Identity puts the user id there.</summary>
    internal static long UserId(HttpContext http) =>
        long.Parse(http.User.FindFirstValue("sub")!, CultureInfo.InvariantCulture);

    private static async Task<IResult> OpenAsync(
        HttpContext http, SiteBook book, DemoBook demos, TunnelHub hub, SiteLogins logins, IAuditLog audit, IOptionsMonitor<SitesOptions> options)
    {
        var settings = options.CurrentValue;
        if (settings.Domain.Length == 0)
            return SitesJson.Error(StatusCodes.Status503ServiceUnavailable, "sites_off", "Sites are not turned on in this server yet.");

        var body = await SitesJson.ReadAsync<OpenSessionRequest>(http);
        var names = body?.Sites ?? [];
        if (body is null || (names.Length == 0 && string.IsNullOrEmpty(body.Demo)))
            return SitesJson.Error(StatusCodes.Status400BadRequest, "bad_request", "Send {\"sites\":[\"name\"]} with one name or more, or {\"demo\":\"new\"}.");
        if (!string.IsNullOrEmpty(body.Demo) && settings.DemoHost.Length == 0)
            return SitesJson.Error(StatusCodes.Status503ServiceUnavailable, "demos_off", "Demos are not turned on in this server yet.");

        var userId = UserId(http);
        var clean = Array.Empty<string>();
        if (names.Length > 0)
        {
            var (error, name, claimed) = await book.ClaimAsync(userId, names, http.RequestAborted);
            if (error != ClaimError.None) return ClaimFailed(error, name, settings);
            clean = claimed;
        }

        // SITE-26: "dalan http --login".
        if (body.Login == true)
        {
            foreach (var site in await logins.TurnOnAsync(userId, clean, http.RequestAborted))
                await audit.RecordAsync(userId, "site.login.on", $"site {site}", null, http.RequestAborted);
        }

        DemoAddress? address = null;
        string[] keys = clean;
        if (!string.IsNullOrEmpty(body.Demo))
        {
            var (error, demo) = await demos.OpenAsync(userId, body.Demo, http.RequestAborted);
            if (demo is null) return DemoFailed(error, settings);
            address = new DemoAddress(demo.Id, settings.DemoUrlOf(demo.Id), demo.ExpiresAt);
            keys = [.. clean, SiteProxy.DemoKey(demo.Id)];
        }

        var session = hub.Open(userId, keys);
        return SitesJson.Ok(new OpenSessionAnswer(session.Id,
            [.. clean.Select(site => new SiteAddress(site, settings.UrlOf(site)))], settings.MaxBodyMb, address,
            settings.StreamChunkKb, settings.StreamWindow));
    }

    internal static IResult ClaimFailed(ClaimError error, string? name, SitesOptions settings) => error switch
    {
        ClaimError.BadName => SitesJson.Error(StatusCodes.Status400BadRequest, "bad_name",
            $"\"{name}\" is not a good site name. Use 3 to {settings.MaxNameLength} letters a-z, digits and '-', not at the start or end. Some names are kept for Dalan."),
        ClaimError.NameTaken => SitesJson.Error(StatusCodes.Status409Conflict, "name_taken",
            $"The name \"{name}\" belongs to another builder. Choose another name."),
        _ => SitesJson.Error(StatusCodes.Status409Conflict, "site_limit",
            $"You may have at most {settings.MaxSitesPerBuilder} sites. Delete one in My apps first."),
    };

    private static IResult DemoFailed(DemoError error, SitesOptions settings) => error switch
    {
        DemoError.Limit => SitesJson.Error(StatusCodes.Status409Conflict, "demo_limit",
            $"You may have at most {settings.MaxDemosPerBuilder} live demos. Stop one first."),
        DemoError.Ended => SitesJson.Error(StatusCodes.Status410Gone, "demo_ended", "This demo has ended."),
        _ => DemoNotFound(),
    };

    private static IResult DemoNotFound() => SitesJson.Error(StatusCodes.Status404NotFound, "demo_not_found", "You have no demo with this id.");

    /// <summary>SITE-20: the owner ends a demo; its address answers 410 at once.</summary>
    private static async Task<IResult> EndDemoAsync(string id, HttpContext http, DemoBook demos, TunnelHub hub)
    {
        if (!await demos.EndAsync(UserId(http), id, http.RequestAborted)) return DemoNotFound();
        hub.Drop(SiteProxy.DemoKey(id));
        return Results.NoContent();
    }

    /// <summary>SITE-23: the agent stops; its visitors get the offline page at once.</summary>
    private static IResult EndSessionAsync(string session, HttpContext http, TunnelHub hub)
    {
        if (hub.Get(session, UserId(http)) is not { } tunnel) return SessionEnded();
        hub.End(tunnel);
        return Results.NoContent();
    }

    /// <summary>
    /// SITE-05: requests out as text messages, answers in as text messages. SITE-15: access is asked again from time to time.
    /// SITE-23: the agent opens a new session after any lost WebSocket, so this session ends with its WebSocket.
    /// </summary>
    private static async Task WebSocketAsync(
        string session, HttpContext http, TunnelHub hub, IUserDirectory users, IOptionsMonitor<SitesOptions> options, TimeProvider time,
        IHostApplicationLifetime lifetime)
    {
        if (!http.WebSockets.IsWebSocketRequest)
        {
            await SitesJson.Error(StatusCodes.Status400BadRequest, "not_websocket", "Open this address as a WebSocket.").ExecuteAsync(http);
            return;
        }

        var userId = UserId(http);
        if (hub.Get(session, userId) is not { } tunnel)
        {
            await SessionEnded().ExecuteAsync(http);
            return;
        }

        using var socket = await http.WebSockets.AcceptWebSocketAsync();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(http.RequestAborted, tunnel.Ended, lifetime.ApplicationStopping);
        // OPS-03: the receive loop has its own token. A cancelled WebSocket receive aborts the socket, and then the close frame
        // (1001 on a stop) could not go out: the agent would see a reset instead.
        using var receiving = CancellationTokenSource.CreateLinkedTokenSource(http.RequestAborted);
        tunnel.StartReading(time.GetUtcNow());
        try
        {
            using var sending = new SemaphoreSlim(1);
            var send = SendAsync(socket, sending, tunnel.Waiting.Reader, request => TunnelHub.IsPending(tunnel, request.Id), stop.Token);
            var control = SendAsync(socket, sending, tunnel.Control.Reader, _ => true, stop.Token);
            var receive = ReceiveAsync(socket, hub, tunnel, options.CurrentValue.MaxBodyBytes, receiving.Token);
            var access = WatchAccessAsync(users, userId, options, time, stop.Token);
            await Task.WhenAny(send, control, receive, access);
            await stop.CancelAsync();
            await Task.WhenAll(Quiet(send), Quiet(control), Quiet(access));
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                // OPS-03: a stop closes with 1001 "going away", so the agent reconnects to the new process.
                var (status, reason) = lifetime.ApplicationStopping.IsCancellationRequested
                    ? (WebSocketCloseStatus.EndpointUnavailable, "server stopping")
                    : (WebSocketCloseStatus.NormalClosure, "session ended");
                await Quiet(socket.CloseOutputAsync(status, reason, CancellationToken.None));
            }
            // The agent hangs up after the close frame; one that does not is cut after the wait.
            receiving.CancelAfter(TimeSpan.FromSeconds(options.CurrentValue.CloseWaitSeconds));
            await Quiet(receive);
        }
        finally
        {
            tunnel.StopReading(time.GetUtcNow());
            hub.End(tunnel);
        }
    }

    /// <summary>Requests and SITE-22 control messages go out on the same socket, one message at a time.</summary>
    private static async Task SendAsync<T>(
        WebSocket socket, SemaphoreSlim sending, ChannelReader<T> messages, Func<T, bool> stillWanted, CancellationToken cancellationToken)
    {
        await foreach (var message in messages.ReadAllAsync(cancellationToken))
        {
            if (!stillWanted(message)) continue;
            var bytes = JsonSerializer.SerializeToUtf8Bytes(message, SitesJson.Options);
            await sending.WaitAsync(cancellationToken);
            try
            {
                await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            }
            finally
            {
                sending.Release();
            }
        }
    }

    private static async Task ReceiveAsync(WebSocket socket, TunnelHub hub, TunnelSession tunnel, long maxBody, CancellationToken cancellationToken)
    {
        // Base64 makes the body a third longer; the rest of the answer is small.
        var maxMessage = maxBody / 3 * 4 + 1024 * 1024;
        var buffer = new ArrayBufferWriter<byte>();
        while (!cancellationToken.IsCancellationRequested)
        {
            buffer.ResetWrittenCount();
            ValueWebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(buffer.GetMemory(64 * 1024), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close) return;
                buffer.Advance(result.Count);
                // A bad agent: too big to be an answer. It reconnects.
                if (buffer.WrittenCount > maxMessage) return;
            }
            while (!result.EndOfMessage);

            // SITE-22: a part over WebSocket never waits here; its ack goes out when it was written.
            if (ReadAnswer(buffer.WrittenSpan) is { } response) await hub.AnswerAsync(tunnel, response, viaWebSocket: true, cancellationToken);
        }
    }

    private static TunnelResponse? ReadAnswer(ReadOnlySpan<byte> json)
    {
        try
        {
            return JsonSerializer.Deserialize<TunnelResponse>(json, SitesJson.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Completes with false when the user may no longer open tunnels.</summary>
    private static async Task<bool> WatchAccessAsync(
        IUserDirectory users, long userId, IOptionsMonitor<SitesOptions> options, TimeProvider time, CancellationToken cancellationToken)
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(options.CurrentValue.AccessCheckSeconds), time, cancellationToken);
            var access = await users.GetAccessAsync(userId, cancellationToken);
            if (access.IsSuspended || !access.IsBuilder) return false;
        }
    }

    private static async Task Quiet(Task task)
    {
        try
        {
            await task;
        }
#pragma warning disable CA1031 // The connection is ending; its errors no longer matter.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    /// <summary>SITE-06: the waiting requests, or an empty list after the wait.</summary>
    private static async Task<IResult> PollAsync(
        string session, HttpContext http, TunnelHub hub, IOptionsMonitor<SitesOptions> options, TimeProvider time,
        IHostApplicationLifetime lifetime)
    {
        if (hub.Get(session, UserId(http)) is not { } tunnel) return SessionEnded();

        var max = options.CurrentValue.PollTimeoutSeconds;
        var wait = int.TryParse(http.Request.Query["timeout"], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            ? Math.Min(seconds, max)
            : max;

        var requests = new List<TunnelRequest>();
        tunnel.StartReading(time.GetUtcNow());
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(wait), time);
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, http.RequestAborted, tunnel.Ended, lifetime.ApplicationStopping);
            while (requests.Count == 0)
            {
                Take(tunnel, requests);
                if (requests.Count > 0 || !await tunnel.Waiting.Reader.WaitToReadAsync(stop.Token)) break;
            }
        }
        catch (OperationCanceledException)
        {
            // The wait is over: return what there is.
        }
        finally
        {
            tunnel.StopReading(time.GetUtcNow());
        }

        return tunnel.IsEnded && requests.Count == 0 ? SessionEnded() : SitesJson.Ok(new PollAnswer([.. requests]));
    }

    private static void Take(TunnelSession tunnel, List<TunnelRequest> requests)
    {
        while (requests.Count < MaxRequestsPerPoll && tunnel.Waiting.Reader.TryRead(out var request))
            if (TunnelHub.IsPending(tunnel, request.Id)) requests.Add(request);
    }

    /// <summary>SITE-06: one answer. SITE-22: a part returns after it was written to the visitor, 410 when its request ended.</summary>
    private static async Task<IResult> AnswerAsync(string session, HttpContext http, TunnelHub hub, IOptionsMonitor<SitesOptions> options)
    {
        if (hub.Get(session, UserId(http)) is not { } tunnel) return SessionEnded();

        var maxBody = options.CurrentValue.MaxBodyBytes;
        if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } size)
            size.MaxRequestBodySize = maxBody / 3 * 4 + 1024 * 1024;

        var response = await SitesJson.ReadAsync<TunnelResponse>(http);
        if (response is null) return SitesJson.Error(StatusCodes.Status400BadRequest, "bad_request", "Send one answer as JSON.");
        bool taken;
        try
        {
            taken = await hub.AnswerAsync(tunnel, response, viaWebSocket: false, http.RequestAborted);
        }
        catch (OperationCanceledException)
        {
            taken = false;
        }

        // A late whole answer is dropped quietly, as before; a late part tells the agent to stop the stream.
        return taken || (!response.More && response.Status != 0)
            ? Results.NoContent()
            : SitesJson.Error(StatusCodes.Status410Gone, "request_gone", "This request has ended. Stop sending its answer.");
    }

    private static IResult SessionEnded() => SitesJson.Error(StatusCodes.Status404NotFound, "session_ended",
        "This tunnel session has ended. Open a new one.");
}

/// <summary>SITE-03 and SITE-14: the access log, then only an approved builder or an admin. Asked on every call.</summary>
internal sealed class BuilderOnly(IUserDirectory users, IAccessLog accessLog) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var userId = TunnelEndpoints.UserId(http);
        await accessLog.RecordAsync(AccessActor.User, userId, http.Connection.RemoteIpAddress, http.Connection.RemotePort, http.RequestAborted);

        var access = await users.GetAccessAsync(userId, http.RequestAborted);
        return access.IsBuilder && !access.IsSuspended
            ? await next(context)
            : SitesJson.Error(StatusCodes.Status403Forbidden, "not_builder",
                "Only an approved builder may run sites. Ask to become a builder in your account screen.");
    }
}
