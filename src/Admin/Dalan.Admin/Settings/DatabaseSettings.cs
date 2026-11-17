using Microsoft.Extensions.Configuration;

namespace Dalan.Admin.Settings;

/// <summary>
/// ADM-07: the settings the admin changed, as the last configuration source, so they win over the file and the
/// environment. Empty until <see cref="SettingsService.LoadAsync"/> reads the table, before the server takes traffic.
/// Every service reads settings through IOptionsMonitor, so a reload reaches them with no other code.
/// </summary>
public sealed class DatabaseSettings : ConfigurationProvider
{
    public void Replace(IDictionary<string, string?> values)
    {
        Data = new Dictionary<string, string?>(values, StringComparer.OrdinalIgnoreCase);
        OnReload();
    }
}

/// <summary>Gives the same provider every time, so the one the services read is the one Admin reloads.</summary>
public sealed class DatabaseSettingsSource : IConfigurationSource
{
    public DatabaseSettings Provider { get; } = new();

    public IConfigurationProvider Build(IConfigurationBuilder builder) => Provider;
}

public static class DatabaseSettingsExtensions
{
    /// <summary>The host calls this after every other configuration source.</summary>
    public static IConfigurationBuilder AddAdminSettings(this IConfigurationBuilder builder) =>
        builder.Add(new DatabaseSettingsSource());
}
