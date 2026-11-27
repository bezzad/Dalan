using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Dalan.Admin.Settings;

public enum SettingKind
{
    Number,
    Switch,
    Text,
}

/// <summary>
/// One editable setting as the admin sees it (ADM-06), with its description (ADM-19). Start and Default are both the value
/// without the panel (the one Reset brings back); Default is the name the settings page uses.
/// </summary>
public sealed record SettingEntry(
    string Key, string Value, string Start, bool Changed, SettingKind Kind, bool Restart,
    string Group, SettingText Name, SettingText Help, string Unit, int? Min, int? Max, bool Risky, string Default);

/// <summary>
/// ADM-06: which settings the panel may change. Computed from the configuration, so a new limit is editable
/// with no extra code. Secrets and start-only safety settings are left out on purpose (design.md, decision 4).
/// </summary>
public sealed class SettingsCatalog(IConfiguration configuration)
{
    private static readonly string[] Sections = ["Admin", "Identity", "Relay", "Notification", "Sites"];

    private static readonly HashSet<string> FromDalan = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dalan:IpRequestLimit", "Dalan:IpLimitWindowSeconds",
    };

    private static readonly HashSet<string> Hidden = new(StringComparer.OrdinalIgnoreCase)
    {
        // Secrets.
        "Identity:SigningKey", "Notification:SmsIr:ApiKey", "Notification:Smtp:Password", "Notification:Push:PrivateKey", "Notification:Push:PublicKey",
        "Identity:GitHub:ClientSecret",
        // A stolen admin session must not open these.
        "Identity:DevLogin", "Identity:AdminPhones", "Notification:SmsProvider", "Notification:LogSmsPhones", "Notification:SmsIr:BaseUrl",
        "Notification:EmailProvider", "Notification:LogEmailAddresses", "Notification:Smtp:Host", "Notification:Smtp:Port",
        "Notification:Smtp:StartTls", "Notification:Smtp:UserName", "Notification:Smtp:From",
        "Identity:GitHub:ClientId", "Identity:GitHub:CallbackUrl", "Identity:GitHub:WebUrl", "Identity:GitHub:ApiUrl",
        // Moving the folder at run time would lose the files.
        "Relay:FileFolder",
        // A stolen admin session must not point every user's install at other files (AGT-10).
        "Sites:AgentFolder",
        // SITE-13 is checked at start: the sites domain must never become Dalan's own (a site could set Dalan's cookies).
        "Sites:Domain",
        // SITE-18: the demo host takes every request of its host; a stolen admin session must not point it at Dalan's own host.
        "Sites:DemoHost",
        // SEC-16: HTML files run script on the view host; a stolen admin session must not point it at Dalan's own host.
        "Relay:FileViewHost",
    };

    public IReadOnlyList<SettingEntry> List() => [.. Keys().Select(Entry)];

    /// <summary>The entry, or null when the key is not editable. The key is matched without regard to case.</summary>
    public SettingEntry? Find(string key) =>
        Keys().FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) is { } name ? Entry(name) : null;

    private IEnumerable<string> Keys() => Sections
        .SelectMany(section => configuration.GetSection(section).AsEnumerable())
        .Select(pair => pair.Key)
        .Concat(FromDalan)
        .Where(key => configuration[key] is not null && !Hidden.Contains(key))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Order(StringComparer.Ordinal);

    private SettingEntry Entry(string key)
    {
        var start = StartValue(key) ?? "";
        var changed = Database()?.TryGet(key, out _) == true;
        // ADM-19: a key with no description still shows, with its key as the name, so a new limit never disappears.
        var d = SettingDescriptions.Find(key)
            ?? new SettingDescription(key.Split(':')[0], new SettingText(key, key), new SettingText("", ""), "", null, null, false, false);
        return new SettingEntry(key, configuration[key]!, start, changed, KindOf(start), d.Restart,
            d.Group, d.Name, d.Help, d.Unit, d.Min, d.Max, d.Risky, start);
    }

    /// <summary>ADM-07: the value as it is stored, or null when it does not fit the kind.</summary>
    public static string? Clean(SettingKind kind, string value, int maxTextLength) => kind switch
    {
        SettingKind.Number => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= 1
            ? number.ToString(CultureInfo.InvariantCulture) : null,
        SettingKind.Switch => bool.TryParse(value, out var on) ? (on ? "true" : "false") : null,
        _ => value.Length <= maxTextLength ? value : null,
    };

    /// <summary>ADM-19: false for a number outside the range of its description.</summary>
    public static bool InRange(SettingEntry entry, string clean) =>
        entry.Kind != SettingKind.Number
        || (int.Parse(clean, CultureInfo.InvariantCulture) is var number && number >= (entry.Min ?? int.MinValue) && number <= (entry.Max ?? int.MaxValue));

    /// <summary>What a value of this kind must be, for the error message.</summary>
    public static string Rule(SettingKind kind, int maxTextLength) => kind switch
    {
        SettingKind.Number => "a whole number from 1 to 2147483647",
        SettingKind.Switch => "true or false",
        _ => $"text of at most {maxTextLength} characters",
    };

    private static SettingKind KindOf(string start) =>
        int.TryParse(start, NumberStyles.None, CultureInfo.InvariantCulture, out _) ? SettingKind.Number
        : bool.TryParse(start, out _) ? SettingKind.Switch
        : SettingKind.Text;

    /// <summary>The value without the panel: the last other source that has it.</summary>
    private string? StartValue(string key)
    {
        foreach (var provider in Root.Providers.Reverse())
        {
            if (provider is not DatabaseSettings && provider.TryGet(key, out var value)) return value;
        }

        return null;
    }

    private DatabaseSettings? Database() => Root.Providers.OfType<DatabaseSettings>().FirstOrDefault();

    private IConfigurationRoot Root => (IConfigurationRoot)configuration;
}
