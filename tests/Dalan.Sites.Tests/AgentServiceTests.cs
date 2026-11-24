using System.Net;
using System.Threading.Channels;
using Dalan.Agent;
using Dalan.Sites.Tunnel;
using Dalan.Testing;
using Microsoft.Extensions.DependencyInjection;
using static Dalan.Sites.Tests.SitesApp;

namespace Dalan.Sites.Tests;

/// <summary>Milestone 15 on the agent: streamed answers, stop, service and status.</summary>
public class AgentServiceTests(PostgresServer postgres)
{
    [Theory]
    [Trait("Rule", "AGT-12")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AGT12_big_file_comes_in_parts_and_arrives_whole(bool webSocket)
    {
        await using var app = await StartAsync(postgres, ("Sites:MaxBodyMb", "1"));
        var (browser, _, key) = await app.BuilderAsync();
        var file = string.Concat(Enumerable.Range(0, 300_000).Select(i => (char)('a' + i % 26)));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        var run = app.RealAgent(key, FakeLocalApp.Text(file), webSocket: webSocket).RunAsync([new("mysite", 3000)], stop.Token);
        await browser.WaitOnlineAsync("mysite");

        var page = await app.Visitor("mysite").GetStringAsync("/big.txt", Cancel);

        Assert.Equal(file.Length, page.Length);
        Assert.Equal(file, page);
        await stop.CancelAsync();
        await run;
    }

    [Theory]
    [Trait("Rule", "AGT-12")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AGT12_event_reaches_the_visitor_before_the_next_one_is_written(bool webSocket)
    {
        await using var app = await StartAsync(postgres);
        var (browser, _, key) = await app.BuilderAsync();
        var events = new EventStream();
        var local = new FakeLocalApp(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(events) }));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        var run = app.RealAgent(key, local, webSocket: webSocket).RunAsync([new("mysite", 3000)], stop.Token);
        await browser.WaitOnlineAsync("mysite");

        events.Write("data: 1\n\n");
        var response = await app.Visitor("mysite").GetAsync("/events", HttpCompletionOption.ResponseHeadersRead, Cancel);
        var stream = await response.Content.ReadAsStreamAsync(Cancel);
        var first = await stream.ReadSomeAsync();
        events.Write("data: 2\n\n");
        var second = await stream.ReadSomeAsync();
        events.End();

        Assert.Equal("data: 1\n\n", first);
        Assert.Equal("data: 2\n\n", second);
        Assert.Equal("", await stream.ReadSomeAsync());
        await stop.CancelAsync();
        await run;
    }

    [Theory]
    [Trait("Rule", "AGT-12")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AGT12_visitor_who_leaves_closes_the_local_answer(bool webSocket)
    {
        await using var app = await StartAsync(postgres);
        var (browser, _, key) = await app.BuilderAsync();
        var events = new EventStream();
        var local = new FakeLocalApp(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(events) }));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        var run = app.RealAgent(key, local, webSocket: webSocket).RunAsync([new("mysite", 3000)], stop.Token);
        await browser.WaitOnlineAsync("mysite");
        using var leave = CancellationTokenSource.CreateLinkedTokenSource(Cancel);

        events.Write("data: 1\n\n");
        var response = await app.Visitor("mysite").GetAsync("/events", HttpCompletionOption.ResponseHeadersRead, leave.Token);
        await (await response.Content.ReadAsStreamAsync(Cancel)).ReadSomeAsync();
        await leave.CancelAsync();
        response.Dispose();
        // Long polling hears of it with the next part.
        for (var i = 0; i < 100 && !events.Closed.IsCompleted; i++)
        {
            events.Write("data: more\n\n");
            await Task.WhenAny(events.Closed, Task.Delay(50, Cancel));
        }

        Assert.True(events.Closed.IsCompleted, "the agent did not close the local answer");
        await stop.CancelAsync();
        await run;
    }

    [Fact]
    [Trait("Rule", "AGT-13")]
    public async Task AGT13_stop_ends_the_session_so_visitors_see_offline_at_once()
    {
        await using var app = await StartAsync(postgres);
        var (browser, _, key) = await app.BuilderAsync();
        var output = new StringWriter();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        var run = app.RealAgent(key, FakeLocalApp.Text("hi"), output, webSocket: false).RunAsync([new("mysite", 3000)], stop.Token);
        await browser.WaitOnlineAsync("mysite");

        await stop.CancelAsync();
        await run;
        var response = await app.Visitor("mysite").GetAsync("/", Cancel);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("site_offline", response.SiteError());
        Assert.Null(app.Services.GetRequiredService<TunnelHub>().Find("mysite"));
        Assert.Contains("offline", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "AGT-14")]
    public void AGT14_linux_writes_a_user_unit_and_enables_it()
    {
        using var setup = new Setup(ServiceSystem.Linux);

        setup.Installer.Install(Saved);
        var unit = File.ReadAllText(setup.Installer.LinuxUnitFile);

        Assert.Contains("ExecStart=\"/opt/dalan/dalan\" start", unit, StringComparison.Ordinal);
        Assert.Contains($"Environment=\"DALAN_CONFIG={setup.Config}\"", unit, StringComparison.Ordinal);
        Assert.Contains("Restart=always", unit, StringComparison.Ordinal);
        Assert.Contains("WantedBy=default.target", unit, StringComparison.Ordinal);
        Assert.Equal(["systemctl --user daemon-reload", "systemctl --user enable dalan", "systemctl --user restart dalan",
            $"loginctl enable-linger {Environment.UserName}"], setup.Calls);
        Assert.Contains("dalan status", setup.Output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "AGT-14")]
    public void AGT14_linux_without_linger_says_so_and_uninstall_removes_the_unit()
    {
        using var setup = new Setup(ServiceSystem.Linux, (file, _) => file == "loginctl" ? 1 : 0);

        setup.Installer.Install(Saved);
        setup.Installer.Uninstall();

        Assert.Contains("only while you are logged in", setup.Output.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(setup.Installer.LinuxUnitFile));
        Assert.Contains("systemctl --user disable --now dalan", setup.Calls);
    }

    [Fact]
    [Trait("Rule", "AGT-14")]
    public void AGT14_windows_uses_the_run_key_and_starts_in_the_background()
    {
        using var setup = new Setup(ServiceSystem.Windows);

        setup.Installer.Install(Saved);

        Assert.Contains(@"reg add HKCU\Software\Microsoft\Windows\CurrentVersion\Run /v Dalan /t REG_SZ /d ""/opt/dalan/dalan"" start --background /f",
            setup.Calls);
        Assert.Equal(["/opt/dalan/dalan start --background"], setup.Started);
    }

    [Fact]
    [Trait("Rule", "AGT-14")]
    public void AGT14_macos_loads_a_launch_agent()
    {
        using var setup = new Setup(ServiceSystem.MacOs);

        setup.Installer.Install(Saved);

        Assert.Contains("<key>KeepAlive</key><true/>", File.ReadAllText(setup.Installer.MacPlistFile), StringComparison.Ordinal);
        Assert.Contains($"launchctl bootstrap gui/0 {setup.Installer.MacPlistFile}", setup.Calls);
    }

    [Fact]
    [Trait("Rule", "AGT-14")]
    public void AGT14_install_with_nothing_saved_says_what_to_run_first()
    {
        using var setup = new Setup(ServiceSystem.Linux);

        var noSite = Assert.Throws<AgentStopException>(() => setup.Installer.Install(Saved with { Sites = [] }));
        var noKey = Assert.Throws<AgentStopException>(() => setup.Installer.Install(Saved with { Key = null }));

        Assert.Contains("dalan http <port> --name <name>", noSite.Message, StringComparison.Ordinal);
        Assert.Contains("dalan login", noKey.Message, StringComparison.Ordinal);
        Assert.Empty(setup.Calls);
    }

    [Fact]
    [Trait("Rule", "AGT-14")]
    public void AGT14_command_line_has_service_status_and_background()
    {
        Assert.Equal(("install", CommandKind.Service), Kind(CommandLine.Parse(["service", "install"])));
        Assert.Equal(("uninstall", CommandKind.Service), Kind(CommandLine.Parse(["service", "uninstall"])));
        Assert.Equal(CommandKind.Status, CommandLine.Parse(["status"]).Kind);
        Assert.True(CommandLine.Parse(["start", "--background"]).Background);
        Assert.Throws<AgentStopException>(() => CommandLine.Parse(["service"]));
        Assert.Contains("dalan service install", CommandLine.Help, StringComparison.Ordinal);

        static (string?, CommandKind) Kind(Command command) => (command.Action, command.Kind);
    }

    [Fact]
    [Trait("Rule", "AGT-15")]
    public async Task AGT15_status_shows_the_service_and_each_site()
    {
        await using var app = await StartAsync(postgres);
        var (browser, _, key) = await app.BuilderAsync();
        await browser.PostAsJsonAsync("/api/sites", new { name = "shop" }, Cancel);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        var run = app.RealAgent(key, FakeLocalApp.Text("hi")).RunAsync([new("mysite", 3000)], stop.Token);
        await browser.WaitOnlineAsync("mysite");
        using var setup = new Setup(ServiceSystem.Linux);

        var sites = await TunnelClient.StatusAsync(app.CreateClient(), key);
        var service = setup.Installer.Status();

        Assert.Contains($"mysite  https://mysite.{Domain}  online", sites, StringComparison.Ordinal);
        Assert.Contains($"shop  https://shop.{Domain}  offline", sites, StringComparison.Ordinal);
        Assert.Equal("Service: not installed (dalan service install)", service);
        await stop.CancelAsync();
        await run;
    }

    private static readonly AgentConfig Saved = new("https://tunnel.test", "dalan_key", [new SiteEntry("mysite", 3000)]);

    /// <summary>A home folder of its own and a recorded system: no real systemctl, launchctl or reg runs.</summary>
    private sealed class Setup : IDisposable
    {
        private readonly string _home = Path.Combine(Path.GetTempPath(), "dalan-tests", Guid.NewGuid().ToString("N"));

        public Setup(ServiceSystem system, Func<string, string[], int>? exitCode = null)
        {
            Config = Path.Combine(_home, ".config", "dalan", "dalan.json");
            Installer = new ServiceInstaller(system, _home, "/opt/dalan/dalan", Config, (file, arguments) =>
            {
                Calls.Add(string.Join(' ', [file, .. arguments]));
                return (exitCode?.Invoke(file, arguments) ?? 0, file == "id" ? "0" : "");
            }, (file, arguments) => Started.Add(string.Join(' ', [file, .. arguments])), Output);
        }

        public string Config { get; }
        public ServiceInstaller Installer { get; }
        public List<string> Calls { get; } = [];
        public List<string> Started { get; } = [];
        public StringWriter Output { get; } = new();

        public void Dispose()
        {
            Output.Dispose();
            if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
        }
    }

    /// <summary>The body of an event stream of the local app: the test writes each event; it knows when the agent closed it.</summary>
    private sealed class EventStream : Stream
    {
        private readonly Channel<byte[]> _events = Channel.CreateUnbounded<byte[]>();
        private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Closed => _closed.Task;

        public void Write(string text) => _events.Writer.TryWrite(System.Text.Encoding.UTF8.GetBytes(text));

        public void End() => _events.Writer.TryComplete();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!await _events.Reader.WaitToReadAsync(cancellationToken)) return 0;
            }
            catch (OperationCanceledException)
            {
                _closed.TrySetResult();
                throw;
            }

            var bytes = await _events.Reader.ReadAsync(cancellationToken);
            bytes.CopyTo(buffer);
            return bytes.Length;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            _closed.TrySetResult();
            base.Dispose(disposing);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
