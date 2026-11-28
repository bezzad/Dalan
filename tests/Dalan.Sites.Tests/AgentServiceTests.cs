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

    private const string WindowsAgent = @"C:\Users\Sara Ahmadi\AppData\Local\dalan\dalan.exe";
    private const string WindowsConfig = @"C:\Users\Sara Ahmadi\AppData\Roaming\dalan\dalan.json";
    private const string WindowsRunKey = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run";

    [Fact]
    [Trait("Rule", "AGT-14")]
    public void AGT14_windows_run_key_has_the_quoted_path_and_the_config_and_the_agent_is_started_and_checked()
    {
        using var setup = Setup.Windows();

        setup.Installer.Install(Saved);
        var output = setup.Output.ToString();

        Assert.Equal(["reg", "add", WindowsRunKey, "/v", "Dalan", "/t", "REG_SZ", "/d",
            $"\"{WindowsAgent}\" start --background --config \"{WindowsConfig}\"", "/f"], setup.Arguments[0]);
        Assert.Equal([WindowsAgent, "start", "--background", "--config", WindowsConfig], setup.StartedArguments.Single());
        Assert.Equal(1, setup.Checks); // Is the started agent still running after a few seconds?
        Assert.Contains(@"Log: C:\Users\Sara Ahmadi\AppData\Roaming\dalan\dalan.log", output, StringComparison.Ordinal);
        Assert.Contains("when you log on to Windows (not at boot)", output, StringComparison.Ordinal);
        Assert.Contains("not started again after a crash", output, StringComparison.Ordinal);
        Assert.Contains("dalan service uninstall", output, StringComparison.Ordinal);
        Assert.Contains("dalan status", output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "AGT-14")]
    public void AGT14_windows_reg_failure_says_not_installed_with_regs_message_and_dalan_start()
    {
        using var setup = Setup.Windows((file, _) => file == "reg" ? (1, "ERROR: Access is denied.") : (0, ""));

        var stop = Assert.Throws<AgentStopException>(() => setup.Installer.Install(Saved));

        Assert.Contains("not installed", stop.Message, StringComparison.Ordinal);
        Assert.Contains("ERROR: Access is denied.", stop.Message, StringComparison.Ordinal);
        Assert.Contains("dalan start", stop.Message, StringComparison.Ordinal);
        Assert.Contains($"/d \"\\\"{WindowsAgent}\\\" start --background", stop.Message, StringComparison.Ordinal); // Quoted as typed.
        Assert.Empty(setup.StartedArguments);
        Assert.DoesNotContain("Done", setup.Output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "AGT-14")]
    public void AGT14_windows_agent_that_cannot_start_or_stops_at_once_points_to_the_log()
    {
        using var cannot = Setup.Windows();
        cannot.StartError = new System.ComponentModel.Win32Exception("The file is blocked.");
        using var stops = Setup.Windows();
        stops.StartedDies = true;

        var notStarted = Assert.Throws<AgentStopException>(() => cannot.Installer.Install(Saved));
        var stopped = Assert.Throws<AgentStopException>(() => stops.Installer.Install(Saved));

        Assert.Contains("The file is blocked.", notStarted.Message, StringComparison.Ordinal);
        Assert.Contains("dalan start", notStarted.Message, StringComparison.Ordinal);
        Assert.Contains(@"C:\Users\Sara Ahmadi\AppData\Roaming\dalan\dalan.log", stopped.Message, StringComparison.Ordinal);
        Assert.Contains("dalan service uninstall", stopped.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Done", stops.Output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "AGT-14")]
    public void AGT14_windows_stops_only_agents_of_the_same_file_and_warns_first()
    {
        using var setup = Setup.Windows();
        setup.Running.AddRange([(11, WindowsAgent.ToUpperInvariant()), (12, @"C:\Tools\dalan.exe"), (Environment.ProcessId, WindowsAgent)]);

        setup.Installer.Install(Saved);

        Assert.Equal(["taskkill /PID 11 /F"], setup.Calls.Where(call => call.StartsWith("taskkill", StringComparison.Ordinal)));
        Assert.Contains("\"dalan http\"", setup.Output.ToString(), StringComparison.Ordinal);
        Assert.Equal(["warning", "taskkill /PID 11 /F"], setup.Log.Where(line => line is "warning" || line.StartsWith("taskkill", StringComparison.Ordinal)));
    }

    [Fact]
    [Trait("Rule", "AGT-14")]
    public void AGT14_windows_status_and_uninstall_look_at_the_run_key_and_this_file_only()
    {
        using var setup = Setup.Windows((file, arguments) => file == "reg" && arguments[0] == "delete" ? (1, "ERROR: Access is denied.") : (0, ""));
        setup.Running.Add((12, @"C:\Tools\dalan.exe"));

        var status = setup.Installer.Status();
        var stop = Assert.Throws<AgentStopException>(setup.Installer.Uninstall);

        Assert.Equal("Service: installed, not running", status);
        Assert.Contains("ERROR: Access is denied.", stop.Message, StringComparison.Ordinal);
        Assert.Equal(["reg", "delete", WindowsRunKey, "/v", "Dalan", "/f"], setup.Arguments.Last());
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
        Assert.Equal(WindowsConfig, CommandLine.Parse(["start", "--background", "--config", WindowsConfig]).Config);
        Assert.Throws<AgentStopException>(() => CommandLine.Parse(["start", "--config"]));
        Assert.Contains("--config <file>", CommandLine.Help, StringComparison.Ordinal);
        Assert.Contains("log on", CommandLine.Help, StringComparison.Ordinal);
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

    /// <summary>A home folder of its own and a recorded system: no real systemctl, launchctl, reg or process runs.</summary>
    private sealed class Setup : IDisposable
    {
        private readonly string _home = Path.Combine(Path.GetTempPath(), "dalan-tests", Guid.NewGuid().ToString("N"));

        public Setup(ServiceSystem system, Func<string, string[], int>? exitCode = null)
            : this(system, "/opt/dalan/dalan", null, (file, arguments) => (exitCode?.Invoke(file, arguments) ?? 0, file == "id" ? "0" : ""))
        {
        }

        private Setup(ServiceSystem system, string agent, string? config, Func<string, string[], (int, string)> answer)
        {
            Config = config ?? Path.Combine(_home, ".config", "dalan", "dalan.json");
            Output = new LogWriter(Log);
            Installer = new ServiceInstaller(system, _home, agent, Config, (file, arguments) =>
            {
                Arguments.Add([file, .. arguments]);
                Calls.Add(string.Join(' ', [file, .. arguments]));
                Log.Add(string.Join(' ', [file, .. arguments]));
                return answer(file, arguments);
            }, (file, arguments) =>
            {
                if (StartError is not null) throw StartError;
                StartedArguments.Add([file, .. arguments]);
                if (!StartedDies) Running.Add((4242, file));
                return 4242;
            }, Output)
            {
                Agents = () => Running,
                Wait = () => Checks++,
            };
        }

        /// <summary>Windows, with the agent in a folder whose name has a space.</summary>
        public static Setup Windows(Func<string, string[], (int, string)>? answer = null) =>
            new(ServiceSystem.Windows, WindowsAgent, WindowsConfig, answer ?? ((_, _) => (0, "")));

        public string Config { get; }
        public ServiceInstaller Installer { get; }
        public List<string> Calls { get; } = [];
        public List<string[]> Arguments { get; } = [];
        public List<string[]> StartedArguments { get; } = [];
        public List<(int Id, string File)> Running { get; } = [];
        public Exception? StartError { get; set; }
        public bool StartedDies { get; set; }
        public int Checks { get; private set; }

        /// <summary>Calls and printed lines in order ("warning" for the line about stopping other agents).</summary>
        public List<string> Log { get; } = [];

        public StringWriter Output { get; }

        public void Dispose()
        {
            Output.Dispose();
            if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
        }

        private sealed class LogWriter(List<string> log) : StringWriter(System.Globalization.CultureInfo.InvariantCulture)
        {
            public override void WriteLine(string? value)
            {
                if (value?.Contains("\"dalan http\"", StringComparison.Ordinal) == true) log.Add("warning");
                base.WriteLine(value);
            }
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
