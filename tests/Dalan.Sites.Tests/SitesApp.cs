using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Agent;
using Dalan.Testing;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Dalan.Sites.Tests;

/// <summary>Short helpers for Sites tests on top of <see cref="DalanApp"/>.</summary>
internal static class SitesApp
{
    public const string Domain = "sites.test";
    public const string AdminPhone = "09129990001";

    public static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public static async Task<DalanApp> StartAsync(PostgresServer postgres, params (string Key, string Value)[] settings)
    {
        var all = new Dictionary<string, string>
        {
            ["Sites:Domain"] = Domain,
            ["Sites:Scheme"] = "https",
            ["Sites:TunnelUrl"] = "https://tunnel.test",
            ["Identity:AdminPhones"] = AdminPhone,
        };
        foreach (var (key, value) in settings) all[key] = value;
        var app = new DalanApp(await postgres.NewDatabaseAsync(), all);
        await app.ReadyAsync();
        return app;
    }

    /// <summary>An approved builder: their browser, user id and an access key.</summary>
    public static async Task<(HttpClient Browser, long UserId, string Key)> BuilderAsync(this DalanApp app, string name = "Builder", bool approve = true)
    {
        var (admin, _) = await app.LoginAsync("Owner", AdminPhone);
        var (browser, userId) = await app.LoginAsync(name);
        if (approve)
            (await admin.PostAsJsonAsync($"/api/admin/users/{userId}/builder", new { approve = true }, Cancel)).EnsureSuccessStatusCode();
        var made = await (await browser.PostAsJsonAsync("/api/account/keys", new { name = "agent" }, Cancel))
            .Content.ReadFromJsonAsync<JsonElement>(Cancel);
        return (browser, userId, made.GetProperty("key").GetString()!);
    }

    public static async Task<HttpClient> AdminAsync(this DalanApp app) => (await app.LoginAsync("Owner", AdminPhone)).Browser;

    /// <summary>A client that calls the tunnel doors with an access key.</summary>
    public static HttpClient Agent(this DalanApp app, string key, string? endPoint = null)
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return endPoint is null ? client : DalanApp.From(client, endPoint);
    }

    /// <summary>A visitor of a site. <paramref name="endPoint"/> is the visitor's own address and port.</summary>
    public static HttpClient Visitor(this DalanApp app, string site, string endPoint = "5.6.7.8:1234")
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri($"https://{site}.{Domain}"),
            AllowAutoRedirect = false,
        });
        return DalanApp.From(client, endPoint);
    }

    public static Task<HttpResponseMessage> OpenAsync(this HttpClient agent, params string[] sites) =>
        agent.PostAsJsonAsync("/api/tunnel/sessions", new { sites }, Cancel);

    public static async Task<string> SessionAsync(this HttpClient agent, params string[] sites)
    {
        var response = await agent.OpenAsync(sites);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Cancel)).GetProperty("session").GetString()!;
    }

    /// <summary>One poll. Returns the requests.</summary>
    public static async Task<JsonElement[]> PollAsync(this HttpClient agent, string session, int timeout = 30)
    {
        var response = await agent.GetAsync($"/api/tunnel/sessions/{session}/requests?timeout={timeout}", Cancel);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancel);
        return [.. body.GetProperty("requests").EnumerateArray()];
    }

    public static Task<HttpResponseMessage> AnswerAsync(this HttpClient agent, string session, long id, int status, string? text = null,
        Dictionary<string, string[]>? headers = null) =>
        agent.PostAsJsonAsync($"/api/tunnel/sessions/{session}/responses", new
        {
            id,
            status,
            headers = headers ?? new Dictionary<string, string[]> { ["Content-Type"] = ["text/plain"] },
            body = text is null ? null : Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text)),
        }, Cancel);

    /// <summary>SITE-22: one message of a streamed answer by long polling. Status 0 is a part after the first.</summary>
    public static Task<HttpResponseMessage> PartAsync(this HttpClient agent, string session, long id, int status, string? text, bool more,
        Dictionary<string, string[]>? headers = null) =>
        agent.PostAsJsonAsync($"/api/tunnel/sessions/{session}/responses", new
        {
            id,
            status,
            headers,
            body = text is null ? null : Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text)),
            more,
        }, Cancel);

    /// <summary>Reads what is there now from a streamed answer, at most <paramref name="max"/> bytes.</summary>
    public static async Task<string> ReadSomeAsync(this Stream stream, int max = 64 * 1024)
    {
        var buffer = new byte[max];
        var read = await stream.ReadAsync(buffer, Cancel);
        return System.Text.Encoding.UTF8.GetString(buffer, 0, read);
    }

    public static async Task<string?> ErrorCodeAsync(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancel);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("message").GetString()), "an error needs a message");
        return body.GetProperty("error").GetString();
    }

    public static string? SiteError(this HttpResponseMessage response) =>
        response.Headers.TryGetValues("Dalan-Error", out var values) ? values.Single() : null;

    /// <summary>The real agent against the test server, with <paramref name="local"/> as the app on the home computer.</summary>
    public static TunnelClient RealAgent(this DalanApp app, string key, HttpMessageHandler local, TextWriter? output = null,
        bool webSocket = true, Action<string>? openBrowser = null)
    {
        var server = app.CreateClient();
        WebSocketConnector connect = webSocket
            ? async (address, agentKey, cancellationToken) =>
            {
                var client = app.Server.CreateWebSocketClient();
                client.ConfigureRequest = request => request.Headers.Authorization = "Bearer " + agentKey;
                try
                {
                    return await client.ConnectAsync(address, cancellationToken);
                }
                catch (InvalidOperationException exception)
                {
                    // The test server says "Incomplete handshake" (for example 499 when the test stops during the connect);
                    // the real ClientWebSocket throws a WebSocketException there.
                    throw new System.Net.WebSockets.WebSocketException(exception.Message, exception);
                }
            }
            : (_, _, _) => throw new System.Net.WebSockets.WebSocketException("blocked on this network");
        return new TunnelClient(server, key, connect, new LocalForwarder(local), output ?? TextWriter.Null)
        {
            FirstWait = TimeSpan.FromMilliseconds(20),
            OpenBrowser = openBrowser,
        };
    }

    /// <summary>Waits until the site is online in the list of its builder.</summary>
    public static async Task WaitOnlineAsync(this HttpClient builder, string site, bool online = true)
    {
        for (var i = 0; i < 500; i++)
        {
            var sites = await builder.GetFromJsonAsync<JsonElement>("/api/sites", Cancel);
            if (sites.GetProperty("sites").EnumerateArray().Any(s => s.GetProperty("name").GetString() == site && s.GetProperty("online").GetBoolean() == online))
                return;
            await Task.Delay(10, Cancel);
        }

        Assert.Fail($"{site} did not become {(online ? "online" : "offline")}");
    }
}

/// <summary>The app on the home computer, in the test: answers with the given function and keeps what it got.</summary>
internal sealed class FakeLocalApp(Func<HttpRequestMessage, Task<HttpResponseMessage>> answer) : HttpMessageHandler
{
    public List<(HttpRequestMessage Request, string Body)> Got { get; } = [];

    public static FakeLocalApp Text(string text) => new(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(text),
    }));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        lock (Got) Got.Add((request, body));
        return await answer(request);
    }
}
