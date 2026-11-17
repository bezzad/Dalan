namespace Dalan.Admin;

/// <summary>Section "Admin" of the settings. Starting values are in appsettings.json; nothing here is a default.</summary>
public sealed class AdminOptions
{
    public int AccessLogIntervalMinutes { get; set; }
    public int AccessLogRetentionDays { get; set; }
    public int AccessLogSweepMinutes { get; set; }
}
