using System.Net;
using System.Net.Http.Json;
using Dalan.Agent;
using Dalan.Sites.Tunnel;
using Dalan.Testing;
using Microsoft.Extensions.DependencyInjection;
using static Dalan.Sites.Tests.SitesApp;

namespace Dalan.Sites.Tests;

public class AgentTests(PostgresServer postgres)
{
    [Fact]
    [Trait("Rule", "AGT-02")]
    public void AGT02_login_saves_the_key_in_a_private_file()
    {
        var path = Path.Combine(Path.GetTempPath(), "dalan-tests", Guid.NewGuid().ToString("N"), "dalan.json");
        var command = CommandLine.Parse(["login", "dalan_abc", "--server", "http://localhost:5000"]);
        var file = new ConfigFile(path);

        file.Save(AgentConfig.Empty with { Key = command.Key, Server = command.Server });

        Assert.Equal("dalan_abc", file.Load().Key);
        Assert.Equal("http://localhost:5000", file.Load().Server);
        if (!OperatingSystem.IsWindows())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }

    [Fact]
    [Trait("Rule", "AGT-02")]
    public void AGT02_key_that_is_not_a_dalan_key_is_refused() =>
        Assert.Throws<AgentStopException>(() => CommandLine.Parse(["login", "123:abc"]));

    [Fact]
    [Trait("Rule", "AGT-03")]
    public void AGT03_command_line_reads_port_and_name_pairs()
    {
        var command = CommandLine.Parse(["http", "3000", "--name", "levelwise", "8080", "--name", "shop", "9000"]);

        Assert.Equal(CommandKind.Http, command.Kind);
        Assert.Equal([(3000, "levelwise"), (8080, "shop"), (9000, null)], command.Sites);
        Assert.Throws<AgentStopException>(() => CommandLine.Parse(["http", "port"]));
        Assert.Throws<AgentStopException>(() => CommandLine.Parse(["http"]));
    }

    [Fact]
    [Trait("Rule", "AGT-03")]
    public void AGT03_port_without_a_name_keeps_the_name_it_got_before()
    {
        var (first, saved) = CommandLine.Resolve([(3000, null)], AgentConfig.Empty);
        var (second, _) = CommandLine.Resolve([(3000, null)], saved);
        var (renamed, renamedConfig) = CommandLine.Resolve([(3000, "Levelwise")], saved);

        Assert.Matches("^[a-z][a-z0-9]{7}$", first[0].Name);
        Assert.Equal(first[0].Name, second[0].Name);
        Assert.Equal(new SiteEntry("levelwise", 3000), Assert.Single(renamed));
        Assert.Equal(new SiteEntry("levelwise", 3000), Assert.Single(renamedConfig.Sites));
    }

    [Fact]
    [Trait("Rule", "AGT-03")]
    public async Task AGT03_one_agent_serves_two_sites_from_their_own_ports()
    {
        await using var app = await StartAsync(postgres);
        var (browser, _, key) = await app.BuilderAsync();
        var local = new FakeLocalApp(request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($"port {request.RequestUri!.Port}"),
        }));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        var run = app.RealAgent(key, local).RunAsync([new("levelwise", 3000), new("shop", 8080)], stop.Token);
        await browser.WaitOnlineAsync("shop");

        var levelwise = await app.Visitor("levelwise").GetStringAsync("/", Cancel);
        var shop = await app.Visitor("shop").GetStringAsync("/", Cancel);

        Assert.Equal("port 3000", levelwise);
        Assert.Equal("port 8080", shop);
        await stop.CancelAsync();
        await run;
    }

    [Fact]
    [Trait("Rule", "AGT-04")]
    public async Task AGT04_start_runs_every_site_of_the_config_file()
    {
        await using var app = await StartAsync(postgres);
        var (browser, _, key) = await app.BuilderAsync();
        var path = Path.Combine(Path.GetTempPath(), "dalan-tests", Guid.NewGuid().ToString("N"), "dalan.json");
        new ConfigFile(path).Save(new AgentConfig(null, key, [new("one", 3001), new("two", 3002)]));
        Assert.Equal(CommandKind.Start, CommandLine.Parse(["start"]).Kind);
        var config = new ConfigFile(path).Load();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Cancel);

        var run = app.RealAgent(config.Key!, FakeLocalApp.Text("hi")).RunAsync(config.Sites, stop.Token);
        await browser.WaitOnlineAsync("one");
        await browser.WaitOnlineAsync("two");

        await stop.CancelAsync();
        await run;
    }

    [Fact]
    [Trait("Rule", "AGT-05")]
    public async Task AGT05_agent_uses_websocket_when_it_works()
    {
        await using var app = await StartAsync(postgres);
        var (browser, _, key) = await app.BuilderAsync();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        var agent = app.RealAgent(key, FakeLocalApp.Text("hello"));

        var run = agent.RunAsync([new("levelwise", 3000)], stop.Token);
        await browser.WaitOnlineAsync("levelwise");
        var page = await app.Visitor("levelwise").GetStringAsync("/", Cancel);

        Assert.Equal("websocket", agent.Transport);
        Assert.Equal("hello", page);
        await stop.CancelAsync();
        await run;
    }

    [Fact]
    [Trait("Rule", "AGT-05")]
    public async Task AGT05_agent_falls_back_to_long_polling_when_websocket_fails()
    {
        await using var app = await StartAsync(postgres);
        var (browser, _, key) = await app.BuilderAsync();
        var output = new StringWriter();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        var agent = app.RealAgent(key, FakeLocalApp.Text("by polling"), output, webSocket: false);

        var run = agent.RunAsync([new("levelwise", 3000)], stop.Token);
        await browser.WaitOnlineAsync("levelwise");
        var page = await app.Visitor("levelwise").GetStringAsync("/", Cancel);

        Assert.Equal("polling", agent.Transport);
        Assert.Equal("by polling", page);
        Assert.Contains("long polling", output.ToString(), StringComparison.Ordinal);
        await stop.CancelAsync();
        await run;
    }

    [Fact]
    [Trait("Rule", "AGT-06")]
    public async Task AGT06_agent_opens_a_new_session_after_a_loss()
    {
        await using var app = await StartAsync(postgres);
        var (browser, _, key) = await app.BuilderAsync();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        var run = app.RealAgent(key, FakeLocalApp.Text("back")).RunAsync([new("levelwise", 3000)], stop.Token);
        await browser.WaitOnlineAsync("levelwise");
        var hub = app.Services.GetRequiredService<TunnelHub>();
        var first = hub.Find("levelwise")!;

        hub.End(first);
        await browser.WaitOnlineAsync("levelwise");

        Assert.NotSame(first, hub.Find("levelwise"));
        Assert.Equal("back", await app.Visitor("levelwise").GetStringAsync("/", Cancel));
        await stop.CancelAsync();
        await run;
    }

    [Fact]
    [Trait("Rule", "AGT-06")]
    public async Task AGT06_refused_key_or_name_stops_the_agent_with_the_message()
    {
        await using var app = await StartAsync(postgres);
        var (_, _, key) = await app.BuilderAsync();

        var badKey = await Assert.ThrowsAsync<AgentStopException>(() =>
            app.RealAgent("dalan_wrong", FakeLocalApp.Text("")).RunAsync([new("levelwise", 3000)], Cancel));
        var badName = await Assert.ThrowsAsync<AgentStopException>(() =>
            app.RealAgent(key, FakeLocalApp.Text("")).RunAsync([new("api", 3000)], Cancel));

        Assert.Contains("key", badKey.Message, StringComparison.Ordinal);
        Assert.Contains("\"api\"", badName.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "AGT-07")]
    public async Task AGT07_request_goes_to_localhost_with_its_headers_and_body()
    {
        await using var app = await StartAsync(postgres);
        var (browser, _, key) = await app.BuilderAsync();
        var local = new FakeLocalApp(_ =>
        {
            var answer = new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("made") };
            answer.Headers.Location = new Uri("/item/1", UriKind.Relative);
            return Task.FromResult(answer);
        });
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        var run = app.RealAgent(key, local).RunAsync([new("levelwise", 3000)], stop.Token);
        await browser.WaitOnlineAsync("levelwise");
        var visitor = app.Visitor("levelwise");
        visitor.DefaultRequestHeaders.Add("X-Custom", "1");

        var response = await visitor.PostAsync("/items?x=1", new StringContent("{\"a\":1}", System.Text.Encoding.UTF8, "application/json"), Cancel);

        var (request, body) = Assert.Single(local.Got);
        Assert.Equal("http://localhost:3000/items?x=1", request.RequestUri!.ToString());
        Assert.Equal("localhost:3000", request.Headers.Host);
        Assert.Equal("1", request.Headers.GetValues("X-Custom").Single());
        Assert.Equal($"levelwise.{Domain}", request.Headers.GetValues("X-Forwarded-Host").Single());
        Assert.Equal("application/json; charset=utf-8", request.Content!.Headers.ContentType!.ToString());
        Assert.Equal("{\"a\":1}", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/item/1", response.Headers.Location!.ToString());
        Assert.Equal("made", await response.Content.ReadAsStringAsync(Cancel));
        await stop.CancelAsync();
        await run;
    }

    [Fact]
    [Trait("Rule", "AGT-07")]
    public async Task AGT07_stopped_local_app_gives_502_that_names_the_port()
    {
        await using var app = await StartAsync(postgres);
        var (browser, _, key) = await app.BuilderAsync();
        var down = new FakeLocalApp(_ => throw new HttpRequestException("Connection refused"));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        var run = app.RealAgent(key, down).RunAsync([new("levelwise", 3000)], stop.Token);
        await browser.WaitOnlineAsync("levelwise");

        var response = await app.Visitor("levelwise").GetAsync("/", Cancel);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Contains("port 3000", await response.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
        await stop.CancelAsync();
        await run;
    }

    [Fact]
    [Trait("Rule", "AGT-07")]
    public async Task AGT07_answer_over_the_limit_becomes_502()
    {
        await using var app = await StartAsync(postgres, ("Sites:MaxBodyMb", "1"));
        var (browser, _, key) = await app.BuilderAsync();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        var run = app.RealAgent(key, FakeLocalApp.Text(new string('x', 1024 * 1024 + 1))).RunAsync([new("levelwise", 3000)], stop.Token);
        await browser.WaitOnlineAsync("levelwise");

        var response = await app.Visitor("levelwise").GetAsync("/", Cancel);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Contains("larger than 1 MB", await response.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
        await stop.CancelAsync();
        await run;
    }

    [Fact]
    [Trait("Rule", "AGT-08")]
    public async Task AGT08_slow_request_does_not_hold_a_fast_one()
    {
        await using var app = await StartAsync(postgres);
        var (browser, _, key) = await app.BuilderAsync();
        var release = new TaskCompletionSource();
        var local = new FakeLocalApp(async request =>
        {
            if (request.RequestUri!.AbsolutePath == "/slow") await release.Task;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(request.RequestUri.AbsolutePath) };
        });
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        var run = app.RealAgent(key, local).RunAsync([new("levelwise", 3000)], stop.Token);
        await browser.WaitOnlineAsync("levelwise");

        var slow = app.Visitor("levelwise").GetStringAsync("/slow", Cancel);
        var fast = await app.Visitor("levelwise").GetStringAsync("/fast", Cancel);

        Assert.Equal("/fast", fast);
        Assert.False(slow.IsCompleted);
        release.SetResult();
        Assert.Equal("/slow", await slow);
        await stop.CancelAsync();
        await run;
    }

    [Fact]
    [Trait("Rule", "AGT-09")]
    public void AGT09_server_comes_from_the_option_then_environment_then_file_then_default()
    {
        var plain = CommandLine.Parse(["start"]);
        var withOption = CommandLine.Parse(["start", "--server", "https://a.example/"]);
        var saved = AgentConfig.Empty with { Server = "https://saved.example" };

        Assert.Equal("https://tunnel.dalanet.ir", CommandLine.ServerOf(plain, AgentConfig.Empty, null));
        Assert.Equal("https://saved.example", CommandLine.ServerOf(plain, saved, ""));
        Assert.Equal("http://localhost:5000", CommandLine.ServerOf(plain, saved, "http://localhost:5000"));
        Assert.Equal("https://a.example", CommandLine.ServerOf(withOption, saved, "http://localhost:5000"));
    }
}
