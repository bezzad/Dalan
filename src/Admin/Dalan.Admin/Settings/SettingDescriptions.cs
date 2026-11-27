using System.Text.Json;

namespace Dalan.Admin.Settings;

/// <summary>A text in both languages of the panel.</summary>
public sealed record SettingText(string Fa, string En);

/// <summary>ADM-19: what the panel says about one setting. Min and max only for an amount.</summary>
public sealed record SettingDescription(
    string Group, SettingText Name, SettingText Help, string Unit, int? Min, int? Max, bool Risky, bool Restart);

/// <summary>ADM-19: settings.json, embedded next to the catalog and keyed by the setting key.</summary>
public static class SettingDescriptions
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<string, SettingDescription> All = Load();

    public static SettingDescription? Find(string key) => All.GetValueOrDefault(key);

    private static Dictionary<string, SettingDescription> Load()
    {
        using var stream = typeof(SettingDescriptions).Assembly.GetManifestResourceStream("Dalan.Admin.settings.json")
            ?? throw new InvalidOperationException("settings.json is not embedded.");
        var all = JsonSerializer.Deserialize<Dictionary<string, SettingDescription>>(stream, Json)
            ?? [];
        return new Dictionary<string, SettingDescription>(all, StringComparer.OrdinalIgnoreCase);
    }
}
