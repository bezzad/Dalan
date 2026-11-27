using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;
using Dalan.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dalan.Host.Tests;

/// <summary>Runs the host on real Kestrel, because shutdown behavior lives in Kestrel.</summary>
public sealed class ShutdownTests(PostgresServer postgres) : IAsyncLifetime
{
    private readonly TaskCompletionSource _releaseSlowRequest = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _slowRequestStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly HoldStop _holdStop = new();
    private const string AdminPhone = "09129990001";
    private DalanApp _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new DalanApp(
            await postgres.NewDatabaseAsync(),
            new Dictionary<string, string>
            {
                ["Dalan:ShutdownTimeoutSeconds"] = "5",
                ["Dalan:StopGraceSeconds"] = "5",
                ["Sites:Domain"] = "sites.test",
                ["Sites:Scheme"] = "https",
                ["Sites:TunnelUrl"] = "https://tunnel.test",
                ["Identity:AdminPhones"] = AdminPhone,
            },
            web => web.ConfigureServices(s =>
            {
                s.AddSingleton<IStartupFilter>(new SlowEndpoint(_slowRequestStarted, _releaseSlowRequest.Task));
                s.AddSingleton<IHostedService>(_holdStop);
            }));
        _factory.UseKestrel(0);
        _factory.StartServer();
        await _factory.ReadyAsync();
    }

    private IHost Host => _factory.Services.GetRequiredService<IHost>();

    [Fact]
    [Trait("Rule", "OPS-02")]
    public async Task OPS02_ready_returns_503_while_stopping()
    {
        var client = _factory.CreateClient();
        var stop = Host.StopAsync(TestContext.Current.CancellationToken);
        await _holdStop.Reached.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        var response = await client.GetAsync("/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        _holdStop.Release();
        await stop;
    }

    [Fact]
    [Trait("Rule", "OPS-03")]
    public async Task OPS03_request_in_flight_finishes_and_server_stops_in_time()
    {
        var client = _factory.CreateClient();
        var slow = client.GetAsync("/test/slow", TestContext.Current.CancellationToken);
        await _slowRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        _holdStop.Release();
        var stop = Host.StopAsync(TestContext.Current.CancellationToken);
        _releaseSlowRequest.SetResult();

        var response = await slow;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await stop.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    /// <summary>A client of the real Kestrel address that sends a session cookie by hand (the cookie is Secure, the address is http).</summary>
    private async Task<HttpClient> PersonAsync(string name, string phone)
    {
        var cancel = TestContext.Current.CancellationToken;
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/dev/login", new { phone, name }, cancel);
        response.EnsureSuccessStatusCode();
        var cookie = string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(line => line.Split(';')[0]));
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        return client;
    }

    [Fact]
    [Trait("Rule", "OPS-03")]
    public async Task OPS03_long_lived_connections_do_not_hold_the_stop()
    {
        var cancel = TestContext.Current.CancellationToken;
        var admin = await PersonAsync("Owner", AdminPhone);
        await _factory.TurnOnTotpAsync(admin);
        var builder = await PersonAsync("Builder", "09121110001");
        var builderId = (await builder.GetFromJsonAsync<JsonElement>("/api/me", cancel)).GetProperty("id").GetInt64();
        (await admin.PostAsJsonAsync($"/api/admin/users/{builderId}/builder", new { approve = true }, cancel)).EnsureSuccessStatusCode();
        var key = (await (await builder.PostAsJsonAsync("/api/account/keys", new { name = "agent" }, cancel))
            .Content.ReadFromJsonAsync<JsonElement>(cancel)).GetProperty("key").GetString()!;
        var token = (await (await builder.PostAsJsonAsync("/api/builder/bots", new { username = "stop_bot", name = "Stop" }, cancel))
            .Content.ReadFromJsonAsync<JsonElement>(cancel)).GetProperty("token").GetString()!;

        // A live stream.
        using var streamResponse = await builder.GetAsync("/api/events", HttpCompletionOption.ResponseHeadersRead, cancel);
        await using var stream = await streamResponse.Content.ReadAsStreamAsync(cancel);
        await stream.ReadAtLeastAsync(new byte[16], 1, cancellationToken: cancel);

        // A bot long poll.
        using var bot = _factory.CreateClient();
        bot.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var poll = bot.GetAsync("/v1/updates?timeout=30", cancel);

        // An agent long poll and an agent WebSocket, each in its own session.
        using var agent = _factory.CreateClient();
        agent.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        var polled = await OpenSessionAsync(agent, "stoppoll");
        var agentPoll = agent.GetAsync($"/api/tunnel/sessions/{polled}/requests?timeout=30", cancel);
        var socketed = await OpenSessionAsync(agent, "stopsock");
        using var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Authorization", "Bearer " + key);
        var address = new UriBuilder(agent.BaseAddress!) { Scheme = "ws", Path = $"/api/tunnel/sessions/{socketed}/ws" }.Uri;
        await socket.ConnectAsync(address, cancel);
        var closing = socket.ReceiveAsync(new byte[1024], cancel);

        await Task.Delay(300, cancel);
        // Read before the stop: the stop disposes the services.
        var grace = TimeSpan.FromSeconds(_factory.Services.GetRequiredService<IConfiguration>().GetValue<int>("Dalan:StopGraceSeconds"));
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _holdStop.Release();
        await Host.StopAsync(cancel).WaitAsync(TimeSpan.FromSeconds(10), cancel);

        Assert.True(stopwatch.Elapsed < grace, $"the stop took {stopwatch.Elapsed}");
        Assert.Equal(WebSocketCloseStatus.EndpointUnavailable, (await closing.WaitAsync(TimeSpan.FromSeconds(5), cancel)).CloseStatus);
        _ = await poll.WaitAsync(TimeSpan.FromSeconds(5), cancel);
        _ = await agentPoll.WaitAsync(TimeSpan.FromSeconds(5), cancel);
    }

    private static async Task<string> OpenSessionAsync(HttpClient agent, string site)
    {
        var response = await agent.PostAsJsonAsync("/api/tunnel/sessions", new { sites = new[] { site } }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("session").GetString()!;
    }

    public async ValueTask DisposeAsync()
    {
        _holdStop.Release();
        _releaseSlowRequest.TrySetResult();
        await _factory.DisposeAsync();
    }

    /// <summary>Stops before the web server does, and waits, so the test can look at the server while it is stopping.</summary>
    private sealed class HoldStop : IHostedService
    {
        private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Reached => _reached.Task;
        public void Release() => _release.TrySetResult();

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _reached.TrySetResult();
            return _release.Task;
        }
    }

    private sealed class SlowEndpoint(TaskCompletionSource started, Task release) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Map("/test/slow", slow => slow.Run(async context =>
            {
                started.TrySetResult();
                await release;
                context.Response.StatusCode = StatusCodes.Status200OK;
            }));
            next(app);
        };
    }
}
