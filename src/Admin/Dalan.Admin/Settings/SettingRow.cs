namespace Dalan.Admin.Settings;

/// <summary>ADM-07: one value the admin set. The key is like "Relay:MaxTextLength".</summary>
public sealed class SettingRow
{
    public required string Key { get; set; }
    public required string Value { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long UpdatedBy { get; set; }
}
