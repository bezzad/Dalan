using System.Globalization;
using System.Security.Cryptography;

namespace Dalan.Agent;

public enum CommandKind { Help, Version, Login, Http, Demo, Start, Service, Status }

/// <summary>
/// What the user asked for. <see cref="Sites"/> holds port and name pairs of "http"; a null name means "choose one".
/// <see cref="Action"/> is "install" or "uninstall" for "service"; <see cref="Background"/> is "start --background" (Windows).
/// </summary>
public sealed record Command(
    CommandKind Kind, string? Key, string? Server, IReadOnlyList<(int Port, string? Name)> Sites, string? Action = null, bool Background = false, bool NoOpen = false);

/// <summary>AGT-02 to AGT-04, AGT-09, AGT-14 and AGT-15: reads the command line.</summary>
public static class CommandLine
{
    public const string DefaultServer = "https://tunnel.dalanet.ir";

    public const string Help = """
        Dalan agent: show a web app on this computer to everyone, with no public IP.

          dalan login <key>                       save your access key (make one in "My apps" on Dalan)
          dalan http <port> [--name <name>]       try it: show the app on this port while this terminal is open (Ctrl+C stops it);
                                                  more "<port> --name <name>" pairs for more sites
          dalan service install                   keep it online: run the saved sites in the background, also after a restart
          dalan service uninstall                 stop that and remove it
          dalan status                            the service, and each site online or offline
          dalan demo <port>                       a test link, for a limited time, to show the app on this port; Ctrl+C ends it
          dalan start                             run every site saved in the config file
          dalan version

          --server <url>   another Dalan server (also the environment DALAN_SERVER)
          --no-open        with "dalan http": do not open the site in the browser (it opens once, on a desktop)
        """;

    public static Command Parse(IReadOnlyList<string> args)
    {
        var rest = new List<string>();
        string? server = null;
        var noOpen = false;
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] == "--no-open") noOpen = true;
            else if (args[i] == "--server") server = i + 1 < args.Count ? args[++i] : throw new AgentStopException("--server needs an address.");
            else rest.Add(args[i]);
        }

        if (rest.Count == 0) return new(CommandKind.Help, null, server, []);
        return rest[0] switch
        {
            "login" when rest.Count == 2 => new(CommandKind.Login, Key(rest[1]), server, []),
            "login" => throw new AgentStopException("Use: dalan login <key>"),
            "http" => new(CommandKind.Http, null, server, Pairs(rest), NoOpen: noOpen),
            "demo" => new(CommandKind.Demo, null, server, [(DemoPort(rest), null)]),
            "start" when rest.Count == 1 => new(CommandKind.Start, null, server, []),
            "start" when rest.Count == 2 && rest[1] == "--background" => new(CommandKind.Start, null, server, [], Background: true),
            "start" => throw new AgentStopException("Use: dalan start"),
            "service" when rest.Count == 2 && rest[1] is "install" or "uninstall" => new(CommandKind.Service, null, server, [], rest[1]),
            "service" => throw new AgentStopException("Use: dalan service install, or dalan service uninstall"),
            "status" => new(CommandKind.Status, null, server, []),
            "version" or "--version" => new(CommandKind.Version, null, server, []),
            "help" or "--help" or "-h" => new(CommandKind.Help, null, server, []),
            _ => throw new AgentStopException($"Unknown command \"{rest[0]}\". Run \"dalan help\"."),
        };
    }

    /// <summary>AGT-09: --server, else DALAN_SERVER, else the saved one, else Dalan's tunnel hostname (never an IP address).</summary>
    public static string ServerOf(Command command, AgentConfig config, string? environment) =>
        (command.Server ?? (environment is { Length: > 0 } ? environment : null) ?? config.Server ?? DefaultServer).TrimEnd('/');

    /// <summary>
    /// AGT-03: each port takes its name, else the name saved for that port, else a new random name.
    /// Returns the sites and the config with them saved.
    /// </summary>
    public static (List<SiteEntry> Sites, AgentConfig Config) Resolve(IReadOnlyList<(int Port, string? Name)> pairs, AgentConfig config)
    {
        var saved = config.Sites.ToList();
        var sites = new List<SiteEntry>();
        foreach (var (port, name) in pairs)
        {
            var site = new SiteEntry(name?.ToLowerInvariant() ?? saved.FirstOrDefault(s => s.Port == port)?.Name ?? RandomName(), port);
            saved.RemoveAll(s => s.Port == site.Port || s.Name == site.Name);
            saved.Add(site);
            sites.Add(site);
        }

        return (sites, config with { Sites = saved });
    }

    /// <summary>8 letters and digits, starting with a letter: a good site name that is hard to guess.</summary>
    public static string RandomName()
    {
        const string letters = "abcdefghijklmnopqrstuvwxyz";
        const string all = letters + "0123456789";
        return letters[RandomNumberGenerator.GetInt32(letters.Length)]
            + RandomNumberGenerator.GetString(all, 7);
    }

    private static string Key(string key) => key.StartsWith("dalan_", StringComparison.Ordinal)
        ? key
        : throw new AgentStopException("This is not a Dalan access key. A key starts with \"dalan_\"; make one in \"My apps\" on Dalan.");

    /// <summary>AGT-11: "demo" takes exactly one port.</summary>
    private static int DemoPort(List<string> rest) =>
        rest.Count == 2 && int.TryParse(rest[1], NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is >= 1 and <= 65535
            ? port
            : throw new AgentStopException("Use: dalan demo <port>, for example dalan demo 3000");

    private static List<(int, string?)> Pairs(List<string> rest)
    {
        var pairs = new List<(int, string?)>();
        for (var i = 1; i < rest.Count; i++)
        {
            if (!int.TryParse(rest[i], NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65535)
                throw new AgentStopException($"\"{rest[i]}\" is not a port. Use: dalan http 3000 --name mysite");
            string? name = null;
            if (i + 1 < rest.Count && rest[i + 1] == "--name")
            {
                if (i + 2 >= rest.Count) throw new AgentStopException("--name needs a name.");
                name = rest[i + 2];
                i += 2;
            }

            pairs.Add((port, name));
        }

        return pairs.Count > 0 ? pairs : throw new AgentStopException("Give the port of your app. Use: dalan http 3000 --name mysite");
    }
}
