using System.Globalization;
using System.Security.Cryptography;

namespace Dalan.Agent;

public enum CommandKind { Help, Version, Login, Http, Start }

/// <summary>What the user asked for. <see cref="Sites"/> holds port and name pairs of "http"; a null name means "choose one".</summary>
public sealed record Command(CommandKind Kind, string? Key, string? Server, IReadOnlyList<(int Port, string? Name)> Sites);

/// <summary>AGT-02 to AGT-04 and AGT-09: reads the command line.</summary>
public static class CommandLine
{
    public const string DefaultServer = "https://tunnel.dalanet.ir";

    public const string Help = """
        Dalan agent: show a web app on this computer to everyone, with no public IP.

          dalan login <key>                       save your access key (make one in "My apps" on Dalan)
          dalan http <port> [--name <name>]       show the app on this port; more "<port> --name <name>" pairs for more sites
          dalan start                             run every site saved in the config file
          dalan version

          --server <url>   another Dalan server (also the environment DALAN_SERVER)
        """;

    public static Command Parse(IReadOnlyList<string> args)
    {
        var rest = new List<string>();
        string? server = null;
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] == "--server") server = i + 1 < args.Count ? args[++i] : throw new AgentStopException("--server needs an address.");
            else rest.Add(args[i]);
        }

        if (rest.Count == 0) return new(CommandKind.Help, null, server, []);
        return rest[0] switch
        {
            "login" when rest.Count == 2 => new(CommandKind.Login, Key(rest[1]), server, []),
            "login" => throw new AgentStopException("Use: dalan login <key>"),
            "http" => new(CommandKind.Http, null, server, Pairs(rest)),
            "start" => new(CommandKind.Start, null, server, []),
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
