using System.Diagnostics;
using System.Security;

namespace Dalan.Agent;

/// <summary>Runs a program of the system and waits: its exit code and what it printed.</summary>
public delegate (int ExitCode, string Output) CommandRunner(string file, params string[] arguments);

/// <summary>Starts a program and does not wait (the agent in the background on Windows).</summary>
public delegate void ProgramStarter(string file, params string[] arguments);

public enum ServiceSystem { Linux, MacOs, Windows }

/// <summary>
/// AGT-14 and AGT-15: the agent as a service of the user, so the saved sites run after a restart. Linux: a systemd user
/// unit; macOS: a launchd agent; Windows: the user's "Run" key (no admin needed). Every system call goes through
/// <see cref="CommandRunner"/>, so tests check the files and commands without changing the machine.
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
            var text = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, text.Trim());
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            return (-1, exception.Message); // The program is not on this system.
        }
    };

    public static ProgramStarter RealStarter => (file, arguments) =>
    {
        using var _ = Process.Start(new ProcessStartInfo(file, arguments) { UseShellExecute = false, CreateNoWindow = true });
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
                Must("reg", "add", WindowsRunKey, "/v", "Dalan", "/t", "REG_SZ", "/d", $"\"{agentFile}\" start --background", "/f");
                StopOtherAgents();
                start(agentFile, "start", "--background");
                break;
        }

        output.WriteLine("Done: your sites now run in the background and start again after a restart.");
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
                run("reg", "delete", WindowsRunKey, "/v", "Dalan", "/f");
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
          <key>StandardOutPath</key><string>{SecurityElement.Escape(Path.Combine(home, "Library", "Logs", "dalan.log"))}</string>
          <key>StandardErrorPath</key><string>{SecurityElement.Escape(Path.Combine(home, "Library", "Logs", "dalan.log"))}</string>
        </dict>
        </plist>

        """;

    private void Must(string file, params string[] arguments)
    {
        var (code, text) = run(file, arguments);
        if (code != 0) throw new AgentStopException($"\"{file} {string.Join(' ', arguments)}\" failed: {text}");
    }

    private static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    /// <summary>Windows: the ids of the other running agents (not this one).</summary>
    private List<string> OtherAgents()
    {
        var self = Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var list = run("tasklist", "/FI", "IMAGENAME eq dalan.exe", "/FO", "CSV", "/NH").Output;
        return [.. list.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split("\",\"")).Where(parts => parts.Length > 1).Select(parts => parts[1]).Where(id => id != self)];
    }

    private void StopOtherAgents()
    {
        foreach (var id in OtherAgents()) run("taskkill", "/PID", id, "/F");
    }
}
