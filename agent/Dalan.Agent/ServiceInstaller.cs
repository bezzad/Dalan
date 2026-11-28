using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Security;

namespace Dalan.Agent;

/// <summary>Runs a program of the system and waits: its exit code and what it printed.</summary>
public delegate (int ExitCode, string Output) CommandRunner(string file, params string[] arguments);

/// <summary>Starts a program and does not wait (the agent in the background on Windows): its process id.</summary>
public delegate int ProgramStarter(string file, params string[] arguments);

public enum ServiceSystem { Linux, MacOs, Windows }

/// <summary>
/// AGT-14 and AGT-15: the agent as a service of the user, so the saved sites run after a restart. Linux: a systemd user
/// unit; macOS: a launchd agent; Windows: the user's "Run" key (no admin needed). Every system call goes through
/// <see cref="CommandRunner"/>, <see cref="ProgramStarter"/> and <see cref="Agents"/>, so tests check the files and
/// commands without changing the machine.
/// </summary>
public sealed class ServiceInstaller(
    ServiceSystem system, string home, string agentFile, string configFile, CommandRunner run, ProgramStarter start, TextWriter output)
{
    public const string Name = "dalan";
    public const string MacLabel = "ir.dalanet.agent";
    private const string WindowsRunKey = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run";

    public static ServiceSystem Current =>
        OperatingSystem.IsWindows() ? ServiceSystem.Windows : OperatingSystem.IsMacOS() ? ServiceSystem.MacOs : ServiceSystem.Linux;

    public string LinuxUnitFile => Path.Combine(home, ".config", "systemd", "user", Name + ".service");

    public string MacPlistFile => Path.Combine(home, "Library", "LaunchAgents", MacLabel + ".plist");

    /// <summary>Windows: dalan.log next to the config file (see Background). Splits on both slashes, so tests run anywhere.</summary>
    public string WindowsLogFile => configFile[..(configFile.LastIndexOfAny(['\\', '/']) + 1)] + "dalan.log";

    /// <summary>Windows: the running programs named like the agent's file, as (id, full path).</summary>
    public Func<IReadOnlyList<(int Id, string File)>> Agents { get; init; } = RealAgents(agentFile);

    /// <summary>Windows: the time the started agent has to fail (wrong key, bad config) before it is checked.</summary>
    public Action Wait { get; init; } = () => Thread.Sleep(TimeSpan.FromSeconds(3));

    public static CommandRunner RealRunner => (file, arguments) =>
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(file, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            })!;
            var error = process.StandardError.ReadToEndAsync(); // Both at once, so a full pipe never blocks the other.
            var text = process.StandardOutput.ReadToEnd() + error.GetAwaiter().GetResult();
            process.WaitForExit();
            return (process.ExitCode, text.Trim());
        }
        catch (Win32Exception exception)
        {
            return (-1, exception.Message); // The program is not on this system.
        }
    };

    public static ProgramStarter RealStarter => (file, arguments) =>
    {
        using var process = Process.Start(new ProcessStartInfo(file, arguments) { UseShellExecute = false, CreateNoWindow = true })!;
        return process.Id;
    };

    /// <summary>The programs named like the agent's file, with their full path; ones we may not read are left out.</summary>
    public static Func<IReadOnlyList<(int Id, string File)>> RealAgents(string agentFile) => () =>
    {
        var list = new List<(int, string)>();
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(agentFile)))
        {
            using (process)
            {
                try
                {
                    if (process.MainModule?.FileName is { } path) list.Add((process.Id, path));
                }
                catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
                {
                    // It ended meanwhile, or it is another user's.
                }
            }
        }

        return list;
    };

    public void Install(AgentConfig config)
    {
        if (config.Key is null) throw new AgentStopException("Log in first: dalan login <key> (make a key in \"My apps\" on Dalan).");
        if (config.Sites.Count == 0)
            throw new AgentStopException("No site saved yet. Try your app once: dalan http <port> --name <name>, then Ctrl+C, then this command.");

        switch (system)
        {
            case ServiceSystem.Linux:
                Write(LinuxUnitFile, LinuxUnit());
                Must("systemctl", "--user", "daemon-reload");
                Must("systemctl", "--user", "enable", Name);
                Must("systemctl", "--user", "restart", Name);
                // Without linger, user services stop at logout and start only at login.
                if (run("loginctl", "enable-linger", Environment.UserName).ExitCode != 0)
                    output.WriteLine("Note: the sites run only while you are logged in. To run them after a restart with no login: sudo loginctl enable-linger " + Environment.UserName);
                break;

            case ServiceSystem.MacOs:
                Write(MacPlistFile, MacPlist());
                var domain = "gui/" + run("id", "-u").Output;
                run("launchctl", "bootout", $"{domain}/{MacLabel}"); // An older one, if any.
                Must("launchctl", "bootstrap", domain, MacPlistFile);
                break;

            default:
                InstallWindows();
                return;
        }

        output.WriteLine("Done: your sites now run in the background and start again after a restart.");
        output.WriteLine(system == ServiceSystem.Linux ? "Log: journalctl --user -u " + Name : "Log: " + MacLogFile);
        output.WriteLine("Stop any \"dalan http\" still open in a terminal (Ctrl+C). Check with: dalan status");
    }

    public void Uninstall()
    {
        switch (system)
        {
            case ServiceSystem.Linux:
                run("systemctl", "--user", "disable", "--now", Name);
                File.Delete(LinuxUnitFile);
                run("systemctl", "--user", "daemon-reload");
                break;

            case ServiceSystem.MacOs:
                run("launchctl", "bootout", $"gui/{run("id", "-u").Output}/{MacLabel}");
                File.Delete(MacPlistFile);
                break;

            default:
                if (run("reg", "query", WindowsRunKey, "/v", "Dalan").ExitCode == 0)
                {
                    var (code, text) = run("reg", "delete", WindowsRunKey, "/v", "Dalan", "/f");
                    if (code != 0)
                        throw new AgentStopException($"The service is not removed: reg delete failed: {text}\n" +
                            $"Remove the value \"Dalan\" by hand with regedit, in {WindowsRunKey}");
                }

                StopOtherAgents();
                break;
        }

        output.WriteLine("The service is removed. Your sites are offline until you run \"dalan start\".");
    }

    /// <summary>AGT-15: one line about the service.</summary>
    public string Status()
    {
        switch (system)
        {
            case ServiceSystem.Linux:
                if (!File.Exists(LinuxUnitFile)) return "Service: not installed (dalan service install)";
                return $"Service: installed, {run("systemctl", "--user", "is-active", Name).Output}";

            case ServiceSystem.MacOs:
                if (!File.Exists(MacPlistFile)) return "Service: not installed (dalan service install)";
                var print = run("launchctl", "print", $"gui/{run("id", "-u").Output}/{MacLabel}");
                return $"Service: installed, {(print.Output.Contains("state = running", StringComparison.Ordinal) ? "running" : "not running")}";

            default:
                if (run("reg", "query", WindowsRunKey, "/v", "Dalan").ExitCode != 0) return "Service: not installed (dalan service install)";
                return $"Service: installed, {(OtherAgents().Count > 0 ? "running" : "not running")}";
        }
    }

    public string LinuxUnit() => $"""
        [Unit]
        Description=Dalan agent: your sites on Dalan
        After=network-online.target

        [Service]
        ExecStart="{agentFile}" start
        Environment="DALAN_CONFIG={configFile}"
        Restart=always
        RestartSec=5

        [Install]
        WantedBy=default.target

        """;

    public string MacPlist() => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
        <plist version="1.0">
        <dict>
          <key>Label</key><string>{MacLabel}</string>
          <key>ProgramArguments</key><array><string>{SecurityElement.Escape(agentFile)}</string><string>start</string></array>
          <key>EnvironmentVariables</key><dict><key>DALAN_CONFIG</key><string>{SecurityElement.Escape(configFile)}</string></dict>
          <key>RunAtLoad</key><true/>
          <key>KeepAlive</key><true/>
          <key>StandardOutPath</key><string>{SecurityElement.Escape(MacLogFile)}</string>
          <key>StandardErrorPath</key><string>{SecurityElement.Escape(MacLogFile)}</string>
        </dict>
        </plist>

        """;

    private string MacLogFile => Path.Combine(home, "Library", "Logs", "dalan.log");

    /// <summary>
    /// The Run key starts the agent at log-on with its config file (the key, the server and the sites are in it). The agent
    /// starts now too, and a few seconds later it must still run.
    /// </summary>
    private void InstallWindows()
    {
        Must("reg", "add", WindowsRunKey, "/v", "Dalan", "/t", "REG_SZ", "/d", $"\"{agentFile}\" start --background --config \"{configFile}\"", "/f");
        StopOtherAgents();
        int id;
        try
        {
            id = start(agentFile, "start", "--background", "--config", configFile);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            throw new AgentStopException($"The Run key is saved, but the agent did not start: {exception.Message}\n" +
                "Run your sites by hand to see why: dalan start. To remove the Run key: dalan service uninstall");
        }

        Wait();
        if (!Agents().Any(agent => agent.Id == id))
            throw new AgentStopException($"The agent started but stopped at once. See why in the log: {WindowsLogFile}\n" +
                "Fix it and run \"dalan service install\" again, or run \"dalan start\" to see it here. To remove the Run key: dalan service uninstall");

        output.WriteLine("Done: your sites now run in the background.");
        output.WriteLine("They start when you log on to Windows (not at boot), and are not started again after a crash.");
        output.WriteLine("Log: " + WindowsLogFile);
        output.WriteLine("Check with: dalan status. Remove with: dalan service uninstall");
    }

    private void Must(string file, params string[] arguments)
    {
        var (code, text) = run(file, arguments);
        if (code != 0)
            throw new AgentStopException($"The service was not installed: {Typed([file, .. arguments])} failed: {text}\n" +
                "Run your sites by hand instead (keep the window open): dalan start");
    }

    /// <summary>A command as one would type it: an argument with a space or a quote is put in quotes.</summary>
    private static string Typed(string[] parts) => string.Join(' ', parts.Select(part =>
        part.Contains(' ', StringComparison.Ordinal) || part.Contains('"', StringComparison.Ordinal)
            ? "\"" + part.Replace("\"", "\\\"", StringComparison.Ordinal) + "\""
            : part));

    private static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    /// <summary>Windows: the other running agents of this same file (not this one, not another copy elsewhere).</summary>
    private List<int> OtherAgents() => [.. Agents()
        .Where(agent => agent.Id != Environment.ProcessId && string.Equals(agent.File, agentFile, StringComparison.OrdinalIgnoreCase))
        .Select(agent => agent.Id)];

    /// <summary>The service runs one agent: older ones of this file stop first, with a word before, as one may be a "dalan http".</summary>
    private void StopOtherAgents()
    {
        var others = OtherAgents();
        if (others.Count == 0) return;
        output.WriteLine($"Stopping {others.Count} other Dalan agent(s) of this file, also any \"dalan http\" open in a terminal.");
        foreach (var id in others) run("taskkill", "/PID", id.ToString(CultureInfo.InvariantCulture), "/F");
    }
}
