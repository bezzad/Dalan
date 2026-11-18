using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Dalan.Admin.Settings;

public enum SettingKind
{
    Number,
    Switch,
    Text,
}

/// <summary>One editable setting as the admin sees it (ADM-06).</summary>
public sealed record SettingEntry(string Key, string Value, string Start, bool Changed, SettingKind Kind, bool Restart);

/// <summary>
/// ADM-06: which settings the panel may change. Computed from the configuration, so a new limit is editable
/// with no extra code. Secrets and start-only safety settings are left out on purpose (design.md, decision 4).
/// </summary>
public sealed class SettingsCatalog(IConfiguration configuration)
{
    private static readonly string[] Sections = ["Admin", "Identity", "Relay", "Notification"];

    private static readonly HashSet<string> FromDalan = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dalan:IpRequestLimit", "Dalan:IpLimitWindowSeconds",
    };

    private static readonly HashSet<string> Hidden = new(StringComparer.OrdinalIgnoreCase)
    {
        // Secrets.
        "Identity:SigningKey", "Notification:Kavenegar:ApiKey", "Notification:Smtp:Password", "Notification:Push:PrivateKey", "Notification:Push:PublicKey",
        "Identity:GitHub:ClientSecret",
        // A stolen admin session must not open these.
        "Identity:DevLogin", "Identity:AdminPhones", "Notification:SmsProvider", "Notification:LogSmsPhones", "Notification:Kavenegar:BaseUrl",
        "Notification:EmailProvider", "Notification:LogEmailAddresses", "Notification:Smtp:Host", "Notification:Smtp:Port",
        "Notification:Smtp:StartTls", "Notification:Smtp:UserName", "Notification:Smtp:From",
        "Identity:GitHub:ClientId", "Identity:GitHub:CallbackUrl", "Identity:GitHub:WebUrl", "Identity:GitHub:ApiUrl",
        // Moving the folder at run time would lose the files.
        "Relay:FileFolder",
    };

    /// <summary>Read only once at start: a change works after a restart.</summary>
    private static readonly HashSet<string> AtStart = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dalan:IpRequestLimit", "Dalan:IpLimitWindowSeconds", "Identity:PasswordHashConcurrency",
        "Notification:Push:QueueSize", "Notification:Push:Workers", "Notification:Push:TimeoutSeconds",
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
        return new SettingEntry(key, configuration[key]!, start, changed, KindOf(start), AtStart.Contains(key));
    }

    /// <summary>ADM-07: the value as it is stored, or null when it does not fit the kind.</summary>
    public static string? Clean(SettingKind kind, string value, int maxTextLength) => kind switch
    {
        SettingKind.Number => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= 1
            ? number.ToString(CultureInfo.InvariantCulture) : null,
        SettingKind.Switch => bool.TryParse(value, out var on) ? (on ? "true" : "false") : null,
        _ => value.Length <= maxTextLength ? value : null,
    };

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
