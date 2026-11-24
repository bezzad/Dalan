using System.Runtime.InteropServices;

namespace Dalan.Agent;

/// <summary>
/// AGT-14: "dalan start --background" (Windows, from the user's "Run" key): no console window, and the output goes to
/// dalan.log next to the config file. The log starts again when it is over 1 MB.
/// </summary>
internal static partial class Background
{
    private const long MaxLogBytes = 1024 * 1024;

    public static void Start(ConfigFile file)
    {
        if (OperatingSystem.IsWindows()) FreeConsole();
        var log = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(file.Path))!, "dalan.log");
        Directory.CreateDirectory(Path.GetDirectoryName(log)!);
        var append = File.Exists(log) && new FileInfo(log).Length < MaxLogBytes;
        var writer = new StreamWriter(log, append) { AutoFlush = true };
        Console.SetOut(writer);
        Console.SetError(writer);
        writer.WriteLine($"--- {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}");
    }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool FreeConsole();
}
