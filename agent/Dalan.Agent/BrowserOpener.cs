using System.Diagnostics;

namespace Dalan.Agent;

/// <summary>AGT-16: opens the address of the site in the default browser, when a person sits at a desktop.</summary>
public static class BrowserOpener
{
    /// <summary>
    /// Only "dalan http" opens, not with --no-open, not when output is not a terminal, and on Linux only on a desktop
    /// (DISPLAY or WAYLAND_DISPLAY) and not under systemd (INVOCATION_ID). The service, "start" and "demo" never open.
    /// </summary>
    public static bool ShouldOpen(CommandKind kind, bool noOpen, bool outputRedirected, bool windows, bool mac, Func<string, string?> environment)
    {
        if (kind != CommandKind.Http || noOpen || outputRedirected) return false;
        if (windows || mac) return true;
        if (!string.IsNullOrEmpty(environment("INVOCATION_ID"))) return false;
        return !string.IsNullOrEmpty(environment("DISPLAY")) || !string.IsNullOrEmpty(environment("WAYLAND_DISPLAY"));
    }

    /// <summary>The program that opens an address: xdg-open on Linux, open on macOS, cmd /c start on Windows.</summary>
    public static ProcessStartInfo StartInfo(string url, bool windows, bool mac)
    {
        var info = new ProcessStartInfo { UseShellExecute = false, CreateNoWindow = true };
        if (windows)
        {
            info.FileName = "cmd";
            info.ArgumentList.Add("/c");
            info.ArgumentList.Add("start");
            info.ArgumentList.Add("");
        }
        else
        {
            info.FileName = mac ? "open" : "xdg-open";
        }

        info.ArgumentList.Add(url);
        return info;
    }

    /// <summary>Opens the address. A failure (no program, no browser) is never an error for the agent: it returns false.</summary>
    public static bool Open(string url, Action<ProcessStartInfo> start)
    {
        try
        {
            start(StartInfo(url, OperatingSystem.IsWindows(), OperatingSystem.IsMacOS()));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>The real opener for <c>TunnelClient.OpenBrowser</c>.</summary>
    public static void OpenReal(string url) => Open(url, info => Process.Start(info)?.Dispose());
}
