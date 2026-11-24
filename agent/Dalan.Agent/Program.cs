using System.Reflection;
using System.Runtime.InteropServices;
using Dalan.Agent;

// The Dalan agent: "dalan login <key>" once, then "dalan http 3000 --name mysite".
try
{
    var command = CommandLine.Parse(args);
    var file = ConfigFile.Default();
    var config = file.Load();
    switch (command.Kind)
    {
        case CommandKind.Help:
            Console.WriteLine(CommandLine.Help);
            return 0;

        case CommandKind.Version:
            Console.WriteLine("dalan " + typeof(CommandLine).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
            return 0;

        case CommandKind.Login:
            file.Save(config with { Key = command.Key, Server = command.Server ?? config.Server });
            Console.WriteLine($"Key saved in {file.Path}. Now run: dalan http <port> --name <name>");
            return 0;

        case CommandKind.Http:
            var (sites, saved) = CommandLine.Resolve(command.Sites, config);
            file.Save(saved);
            return await RunAsync(command, saved, sites);

        case CommandKind.Service:
            var installer = Installer(file);
            if (command.Action == "install") installer.Install(config);
            else installer.Uninstall();
            return 0;

        case CommandKind.Status:
            Console.WriteLine(Installer(file).Status());
            return await StatusAsync(command, config);

        case CommandKind.Demo:
            // AGT-11: a demo is not saved in the config file.
            return await RunAsync(command, config, [], command.Sites[0].Port);

        default:
            if (command.Background) Background.Start(file);
            return config.Sites.Count > 0
                ? await RunAsync(command, config, config.Sites)
                : throw new AgentStopException($"No site in {file.Path} yet. Run once: dalan http <port> --name <name>");
    }
}
catch (AgentStopException stop)
{
    Console.Error.WriteLine(stop.Message);
    return 1;
}

// AGT-14: the service runs this same file with "start" and the same config file.
static ServiceInstaller Installer(ConfigFile file) => new(ServiceInstaller.Current,
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Environment.ProcessPath!, Path.GetFullPath(file.Path),
    ServiceInstaller.RealRunner, ServiceInstaller.RealStarter, Console.Out);

// AGT-15: the sites of the key's builder, as "My apps" lists them.
static async Task<int> StatusAsync(Command command, AgentConfig config)
{
    if (config.Key is null) throw new AgentStopException("Log in first: dalan login <key> (make a key in \"My apps\" on Dalan).");
    var server = CommandLine.ServerOf(command, config, Environment.GetEnvironmentVariable("DALAN_SERVER"));
    using var http = new HttpClient { BaseAddress = new Uri(server), Timeout = TimeSpan.FromSeconds(20) };
    Console.WriteLine(await TunnelClient.StatusAsync(http, config.Key));
    return 0;
}

static async Task<int> RunAsync(Command command, AgentConfig config, IReadOnlyList<SiteEntry> sites, int? demoPort = null)
{
    if (config.Key is null) throw new AgentStopException("Log in first: dalan login <key> (make a key in \"My apps\" on Dalan).");

    using var stop = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        stop.Cancel();
    };
    // AGT-13: the service manager (systemd, launchd) and a shutdown stop the agent with SIGTERM; end the session then too.
    using var term = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
    {
        context.Cancel = true;
        stop.Cancel();
    });

    var server = CommandLine.ServerOf(command, config, Environment.GetEnvironmentVariable("DALAN_SERVER"));
    using var http = new HttpClient { BaseAddress = new Uri(server), Timeout = TimeSpan.FromMinutes(2) };
    using var handler = LocalForwarder.RealHandler();
    using var local = new LocalForwarder(handler);
    // AGT-16: "dalan http" on a desktop opens the site once; the service, start and demo never do.
    var opener = BrowserOpener.ShouldOpen(command.Kind, command.NoOpen, Console.IsOutputRedirected, OperatingSystem.IsWindows(),
        OperatingSystem.IsMacOS(), Environment.GetEnvironmentVariable);
    var client = new TunnelClient(http, config.Key, TunnelClient.RealWebSocket, local, Console.Out)
    {
        OpenBrowser = opener ? BrowserOpener.OpenReal : null,
    };
    Console.WriteLine($"Connecting to {server} ...");
    await client.RunAsync(sites, stop.Token, demoPort);
    return 0;
}
