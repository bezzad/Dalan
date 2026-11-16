using System.Net;
using Dalan.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dalan.Host.Tests;

/// <summary>Runs the host on real Kestrel, because shutdown behavior lives in Kestrel.</summary>
public sealed class ShutdownTests(PostgresServer postgres) : IAsyncLifetime
{
    private readonly TaskCompletionSource _releaseSlowRequest = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _slowRequestStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly HoldStop _holdStop = new();
    private DalanApp _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new DalanApp(
            await postgres.NewDatabaseAsync(),
            new Dictionary<string, string> { ["Dalan:ShutdownTimeoutSeconds"] = "5" },
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
