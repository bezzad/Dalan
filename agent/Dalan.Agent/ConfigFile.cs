using System.Text.Json;

namespace Dalan.Agent;

/// <summary>AGT-02: the agent's settings on disk, readable only by the user on Linux and macOS.</summary>
public sealed class ConfigFile(string path)
{
    /// <summary>~/.config/dalan/dalan.json, or %APPDATA%\dalan\dalan.json on Windows. DALAN_CONFIG names another file.</summary>
    public static ConfigFile Default() => new(Environment.GetEnvironmentVariable("DALAN_CONFIG") is { Length: > 0 } path
        ? path
        : System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "dalan", "dalan.json"));

    public string Path { get; } = path;

    public AgentConfig Load()
    {
        if (!File.Exists(Path)) return AgentConfig.Empty;
        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(Path), AgentJson.Default.AgentConfig) ?? AgentConfig.Empty;
        }
        catch (JsonException)
        {
            throw new AgentStopException($"The config file {Path} is not valid JSON. Fix it or delete it and run \"dalan login\" again.");
        }
    }

    public void Save(AgentConfig config)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!);
        // Made empty and private first, so the key is never in a file others can read.
        using (File.Create(Path)) { }
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(Path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.WriteAllText(Path, JsonSerializer.Serialize(config, AgentJson.Default.AgentConfig));
    }
}

/// <summary>An error the user must fix; the agent stops with this message.</summary>
public sealed class AgentStopException(string message) : Exception(message);
