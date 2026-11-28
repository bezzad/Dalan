using System.Globalization;
using Dalan.Admin.Settings;
using Microsoft.Extensions.Configuration;

namespace Dalan.Admin.Tests;

/// <summary>ADM-19: the descriptions in settings.json against the real appsettings.json. No database needed.</summary>
public class SettingDescriptionTests
{
    private static readonly string[] Groups = ["Identity", "Relay", "Notification", "Sites", "Admin", "Dalan"];

    private static readonly string[] Units =
        ["", "seconds", "minutes", "hours", "days", "MB", "KB", "bytes", "bits", "characters", "digits", "tries", "passes", "rows",
         "keys", "codes", "steps", "devices", "bots", "sites", "demos", "messages", "updates", "events", "buttons", "commands",
         "connections", "parts", "requests", "addresses"];

    /// <summary>Numbers that are ids, not amounts: no range.</summary>
    private static readonly string[] NoRange = ["Notification:SmsIr:TemplateId", "Notification:SmsIr:NoticeTemplateId"];

    /// <summary>The catalog as the server builds it from appsettings.json alone.</summary>
    private static IReadOnlyList<SettingEntry> Catalog()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Dalan.slnx"))) dir = dir.Parent;
        var file = Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Dalan.slnx not found."), "src", "Dalan.Host", "appsettings.json");
        var configuration = new ConfigurationBuilder().AddJsonFile(file).AddAdminSettings().Build();
        return new SettingsCatalog(configuration).List();
    }

    [Fact]
    [Trait("Rule", "ADM-19")]
    public void ADM19_every_catalog_key_has_a_description()
    {
        var missing = Catalog().Where(e => SettingDescriptions.Find(e.Key) is null).Select(e => e.Key).ToList();

        Assert.True(missing.Count == 0, "These settings need a description in settings.json: " + string.Join(", ", missing));
    }

    [Fact]
    [Trait("Rule", "ADM-19")]
    public void ADM19_descriptions_are_complete_and_hold_the_start_value()
    {
        var errors = new List<string>();
        foreach (var entry in Catalog())
        {
            if (SettingDescriptions.Find(entry.Key) is not { } d) continue;
            if (!Groups.Contains(d.Group) || !entry.Key.StartsWith(d.Group + ":", StringComparison.Ordinal))
                errors.Add($"{entry.Key}: group {d.Group}");
            if (string.IsNullOrWhiteSpace(d.Name.Fa) || string.IsNullOrWhiteSpace(d.Name.En)
                || string.IsNullOrWhiteSpace(d.Help.Fa) || string.IsNullOrWhiteSpace(d.Help.En))
                errors.Add($"{entry.Key}: name and help need both languages");
            if (!Units.Contains(d.Unit)) errors.Add($"{entry.Key}: unit {d.Unit}");

            if (entry.Kind != SettingKind.Number || NoRange.Contains(entry.Key))
            {
                if (d.Min is not null || d.Max is not null) errors.Add($"{entry.Key}: a range only for an amount");
                continue;
            }

            var start = int.Parse(entry.Start, CultureInfo.InvariantCulture);
            if (d.Min is not { } min || d.Max is not { } max) errors.Add($"{entry.Key}: a number needs min and max");
            else if (min < 1 || min > start || start > max) errors.Add($"{entry.Key}: {start} is not in {min} to {max}");
        }

        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }
}
