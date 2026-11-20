using System.Buffers;
using System.Globalization;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text.Json;
using Dalan.Admin.Contracts;
using Dalan.Identity.Contracts;
using Dalan.Sites.Names;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
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
        tunnel.MapGet("/sessions/{session}/ws", WebSocketAsync);
        tunnel.MapGet("/sessions/{session}/requests", PollAsync);
        tunnel.MapPost("/sessions/{session}/responses", AnswerAsync);
    }

    /// <summary>The user of the session or key. "sub" is the standard JWT claim; Identity puts the user id there.</summary>
    internal static long UserId(HttpContext http) =>
        long.Parse(http.User.FindFirstValue("sub")!, CultureInfo.InvariantCulture);

    private static async Task<IResult> OpenAsync(HttpContext http, SiteBook book, TunnelHub hub, IOptionsMonitor<SitesOptions> options)
    {
        var settings = options.CurrentValue;
        if (settings.Domain.Length == 0)
            return SitesJson.Error(StatusCodes.Status503ServiceUnavailable, "sites_off", "Sites are not turned on in this server yet.");

        var body = await SitesJson.ReadAsync<OpenSessionRequest>(http);
        if (body?.Sites is not { Length: > 0 } names)
            return SitesJson.Error(StatusCodes.Status400BadRequest, "bad_request", "Send {\"sites\":[\"name\"]} with one name or more.");

        var userId = UserId(http);
        var (error, name, clean) = await book.ClaimAsync(userId, names, http.RequestAborted);
        return error switch
        {
            ClaimError.BadName => SitesJson.Error(StatusCodes.Status400BadRequest, "bad_name",
                $"\"{name}\" is not a good site name. Use 3 to {settings.MaxNameLength} letters a-z, digits and '-', not at the start or end. Some names are kept for Dalan."),
            ClaimError.NameTaken => SitesJson.Error(StatusCodes.Status409Conflict, "name_taken",
                $"The name \"{name}\" belongs to another builder. Choose another name."),
            ClaimError.SiteLimit => SitesJson.Error(StatusCodes.Status409Conflict, "site_limit",
                $"You may have at most {settings.MaxSitesPerBuilder} sites. Delete one in My apps first."),
            _ => SitesJson.Ok(Opened(hub.Open(userId, clean), clean, settings)),
        };
    }

    private static OpenSessionAnswer Opened(TunnelSession session, string[] sites, SitesOptions settings) =>
        new(session.Id, [.. sites.Select(site => new SiteAddress(site, settings.UrlOf(site)))], settings.MaxBodyMb);

    /// <summary>SITE-05: requests out as text messages, answers in as text messages. SITE-15: access is asked again from time to time.</summary>
    private static async Task WebSocketAsync(
        string session, HttpContext http, TunnelHub hub, IUserDirectory users, IOptionsMonitor<SitesOptions> options, TimeProvider time)
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
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(http.RequestAborted, tunnel.Ended);
        tunnel.StartReading(time.GetUtcNow());
        try
        {
            var send = SendAsync(socket, tunnel, stop.Token);
            var receive = ReceiveAsync(socket, tunnel, options.CurrentValue.MaxBodyBytes, stop.Token);
            var access = WatchAccessAsync(users, userId, options, time, stop.Token);
            await Task.WhenAny(send, receive, access);
            if (access.IsCompletedSuccessfully && !await access) hub.End(tunnel);
            await stop.CancelAsync();
            await Task.WhenAll(Quiet(send), Quiet(receive), Quiet(access));
            if (socket.State == WebSocketState.Open)
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "session ended", CancellationToken.None);
        }
        finally
        {
            tunnel.StopReading(time.GetUtcNow());
        }
    }

    private static async Task SendAsync(WebSocket socket, TunnelSession tunnel, CancellationToken cancellationToken)
    {
        await foreach (var request in tunnel.Waiting.Reader.ReadAllAsync(cancellationToken))
        {
            if (!TunnelHub.IsPending(tunnel, request.Id)) continue;
            var bytes = JsonSerializer.SerializeToUtf8Bytes(request, SitesJson.Options);
            await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
        }
    }

    private static async Task ReceiveAsync(WebSocket socket, TunnelSession tunnel, long maxBody, CancellationToken cancellationToken)
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

            if (ReadAnswer(buffer.WrittenSpan) is { } response) TunnelHub.Answer(tunnel, response);
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
        string session, HttpContext http, TunnelHub hub, IOptionsMonitor<SitesOptions> options, TimeProvider time)
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
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, http.RequestAborted, tunnel.Ended);
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

    /// <summary>SITE-06: one answer.</summary>
    private static async Task<IResult> AnswerAsync(string session, HttpContext http, TunnelHub hub, IOptionsMonitor<SitesOptions> options)
    {
        if (hub.Get(session, UserId(http)) is not { } tunnel) return SessionEnded();

        var maxBody = options.CurrentValue.MaxBodyBytes;
        if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } size)
            size.MaxRequestBodySize = maxBody / 3 * 4 + 1024 * 1024;

        var response = await SitesJson.ReadAsync<TunnelResponse>(http);
        if (response is null) return SitesJson.Error(StatusCodes.Status400BadRequest, "bad_request", "Send one answer as JSON.");
        TunnelHub.Answer(tunnel, response);
        return Results.NoContent();
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
