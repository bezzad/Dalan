using System.Buffers;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;

namespace Dalan.Agent;

/// <summary>Opens a WebSocket to the server with the access key.</summary>
public delegate Task<WebSocket> WebSocketConnector(Uri address, string key, CancellationToken cancellationToken);

/// <summary>
/// AGT-05 to AGT-08: keeps the sites online. Opens a session, serves it over WebSocket, or by long polling when
/// WebSocket does not work, and starts again after a loss.
/// </summary>
public sealed class TunnelClient(
    HttpClient server, string key, WebSocketConnector connectWebSocket, LocalForwarder local, TextWriter output)
{
    private const int PollSeconds = 30;
    private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(30);

    /// <summary>The wait before the first new try; it doubles up to 30 seconds (AGT-06).</summary>
    public TimeSpan FirstWait { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>How the last connection was served: "websocket" or "polling" (for tests and the status line).</summary>
    public string? Transport { get; private set; }

    public static WebSocketConnector RealWebSocket => async (address, key, cancellationToken) =>
    {
        var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Authorization", "Bearer " + key);
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
        try
        {
            await socket.ConnectAsync(address, cancellationToken);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    };

    /// <summary>AGT-11: the id of the demo this client keeps, once the server gave it.</summary>
    private string? _demoId;

    /// <summary>
    /// Keeps the sites online until <paramref name="cancellationToken"/> is cancelled. With <paramref name="demoPort"/> it also
    /// serves a demo (AGT-11): Ctrl+C ends it. A demo that the server ended stops the run with an <see cref="AgentStopException"/>.
    /// </summary>
    public async Task RunAsync(IReadOnlyList<SiteEntry> sites, CancellationToken cancellationToken, int? demoPort = null)
    {
        await KeepOnlineAsync(sites, demoPort, cancellationToken);
        if (_demoId is not null) await EndDemoAsync();
    }

    private async Task KeepOnlineAsync(IReadOnlyList<SiteEntry> sites, int? demoPort, CancellationToken cancellationToken)
    {
        var ports = sites.ToDictionary(s => s.Name.ToLowerInvariant(), s => s.Port);
        var wait = FirstWait;
        var first = true;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var session = await OpenAsync(sites, demoPort is null ? null : _demoId ?? "new", cancellationToken);
                wait = FirstWait;
                if (session.Demo is { } demo && demoPort is { } port)
                {
                    _demoId = demo.Id;
                    ports[$"demo:{demo.Id}"] = port;
                }

                if (first) PrintSites(session, sites, demoPort);
                first = false;
                await ServeAsync(session, ports, cancellationToken);
                output.WriteLine("Connection to Dalan ended. Connecting again...");
            }
            catch (Exception exception) when (cancellationToken.IsCancellationRequested && exception is OperationCanceledException
                or HttpRequestException or WebSocketException or IOException)
            {
                return; // Ctrl+C.
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested && exception is HttpRequestException
                or WebSocketException or IOException or TaskCanceledException or JsonException)
            {
                output.WriteLine($"Cannot reach Dalan ({exception.Message}). Trying again in {wait.TotalSeconds:0} s.");
            }

            try
            {
                await Task.Delay(wait, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            wait = wait * 2 < MaxWait ? wait * 2 : MaxWait;
        }
    }

    private async Task<SessionOpened> OpenAsync(IReadOnlyList<SiteEntry> sites, string? demo, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/tunnel/sessions")
        {
            Content = JsonContent.Create(new OpenSession([.. sites.Select(s => s.Name)], demo), AgentJson.Default.OpenSession),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var response = await server.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync(AgentJson.Default.SessionOpened, cancellationToken)
                ?? throw new JsonException("empty answer");

        // AGT-06: a refused key or name is for the user to fix; trying again does not help.
        if (response.StatusCode is HttpStatusCode.Unauthorized)
            throw new AgentStopException("Dalan did not accept your key. Make a new one in \"My apps\" and run \"dalan login <key>\".");
        if ((int)response.StatusCode is >= 400 and < 500)
            throw new AgentStopException(await MessageAsync(response, cancellationToken));
        throw new HttpRequestException($"server answered {(int)response.StatusCode}");
    }

    private static async Task<string> MessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync(AgentJson.Default.ServerError, cancellationToken);
            return error?.Message ?? $"Dalan refused ({(int)response.StatusCode}).";
        }
        catch (JsonException)
        {
            return $"Dalan refused ({(int)response.StatusCode}).";
        }
    }

    private void PrintSites(SessionOpened session, IReadOnlyList<SiteEntry> sites, int? demoPort)
    {
        if (session.Demo is { } demo)
        {
            output.WriteLine($"Demo: {demo.Url}  ->  http://localhost:{demoPort}");
            output.WriteLine($"It ends at {demo.ExpiresAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC.");
            output.WriteLine("A demo is for tests: do not use real accounts or secrets in it");
        }

        foreach (var site in session.Sites)
        {
            var port = sites.First(s => string.Equals(s.Name, site.Name, StringComparison.OrdinalIgnoreCase)).Port;
            output.WriteLine($"Online: {site.Url}  ->  http://localhost:{port}");
        }

        output.WriteLine(_demoId is null ? "Press Ctrl+C to stop." : "Press Ctrl+C to stop and end the demo.");
    }

    /// <summary>AGT-11: asks the server to end the demo. The demo ends by its time anyway, so a failure is only said.</summary>
    private async Task EndDemoAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/tunnel/demos/{_demoId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            using var response = await server.SendAsync(request, limit.Token);
            output.WriteLine(response.IsSuccessStatusCode ? "Demo ended." : $"Could not end the demo ({(int)response.StatusCode}); it ends by its time.");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            output.WriteLine($"Could not end the demo ({exception.Message}); it ends by its time.");
        }
    }

    /// <summary>AGT-05: WebSocket first; when it cannot be opened, long polling on the same session.</summary>
    private async Task ServeAsync(SessionOpened session, Dictionary<string, int> ports, CancellationToken cancellationToken)
    {
        var maxBody = session.MaxBodyMb * 1024L * 1024L;
        WebSocket socket;
        try
        {
            socket = await connectWebSocket(WebSocketAddress(session.Session), key, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested && exception is WebSocketException
            or HttpRequestException or IOException or InvalidOperationException)
        {
            if (Transport != "polling") output.WriteLine("WebSocket does not work on this network; using long polling.");
            Transport = "polling";
            await PollAsync(session.Session, ports, maxBody, cancellationToken);
            return;
        }

        Transport = "websocket";
        using (socket) await WebSocketAsync(socket, ports, maxBody, cancellationToken);
    }

    private Uri WebSocketAddress(string session)
    {
        var builder = new UriBuilder(new Uri(server.BaseAddress!, $"/api/tunnel/sessions/{session}/ws"));
        builder.Scheme = builder.Scheme == "https" ? "wss" : "ws";
        return builder.Uri;
    }

    private async Task WebSocketAsync(WebSocket socket, Dictionary<string, int> ports, long maxBody, CancellationToken cancellationToken)
    {
        using var sending = new SemaphoreSlim(1);
        var buffer = new ArrayBufferWriter<byte>();
        while (socket.State == WebSocketState.Open)
        {
            buffer.ResetWrittenCount();
            ValueWebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(buffer.GetMemory(64 * 1024), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close) return;
                buffer.Advance(result.Count);
            }
            while (!result.EndOfMessage);

            var request = JsonSerializer.Deserialize(buffer.WrittenSpan, AgentJson.Default.TunnelRequest);
            if (request is null) continue;
            // AGT-08: each request on its own, so a slow one does not hold the others.
            _ = Task.Run(async () =>
            {
                var response = await HandleAsync(request, ports, maxBody, cancellationToken);
                var bytes = JsonSerializer.SerializeToUtf8Bytes(response, AgentJson.Default.TunnelResponse);
                await sending.WaitAsync(cancellationToken);
                try
                {
                    await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
                }
                finally
                {
                    sending.Release();
                }
            }, cancellationToken);
        }
    }

    private async Task PollAsync(string session, Dictionary<string, int> ports, long maxBody, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/tunnel/sessions/{session}/requests?timeout={PollSeconds}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            using var response = await server.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) return; // session_ended: open a new one.
            response.EnsureSuccessStatusCode();
            var answer = await response.Content.ReadFromJsonAsync(AgentJson.Default.PollAnswer, cancellationToken);
            foreach (var visitor in answer?.Requests ?? [])
                _ = Task.Run(() => AnswerByPostAsync(session, visitor, ports, maxBody, cancellationToken), cancellationToken);
        }
    }

    private async Task AnswerByPostAsync(
        string session, TunnelRequest visitor, Dictionary<string, int> ports, long maxBody, CancellationToken cancellationToken)
    {
        var response = await HandleAsync(visitor, ports, maxBody, cancellationToken);
        using var post = new HttpRequestMessage(HttpMethod.Post, $"/api/tunnel/sessions/{session}/responses")
        {
            Content = JsonContent.Create(response, AgentJson.Default.TunnelResponse),
        };
        post.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        try
        {
            using var _ = await server.SendAsync(post, cancellationToken);
        }
        catch (HttpRequestException)
        {
            // The visitor gets a timeout; the poll loop sees the lost connection.
        }
    }

    private Task<TunnelResponse> HandleAsync(TunnelRequest request, Dictionary<string, int> ports, long maxBody, CancellationToken cancellationToken) =>
        ports.TryGetValue(request.Site, out var port)
            ? local.ForwardAsync(request, port, maxBody, cancellationToken)
            : Task.FromResult(LocalForwarder.Page(request.Id, HttpStatusCode.NotFound, $"This agent does not serve the site {request.Site}."));
}
