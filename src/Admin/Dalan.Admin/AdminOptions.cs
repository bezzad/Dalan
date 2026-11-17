namespace Dalan.Admin;

/// <summary>Section "Admin" of the settings. Starting values are in appsettings.json; nothing here is a default.</summary>
public sealed class AdminOptions
{
    public int AccessLogIntervalMinutes { get; set; }
    public int AccessLogRetentionDays { get; set; }
    public int AccessLogSweepMinutes { get; set; }
    public int PageSize { get; set; }
    public int MaxSettingLength { get; set; }

    /// <summary>ADM-15: the address of the site, for llms.txt and the ready prompt.</summary>
    public string SiteUrl { get; set; } = "";

    /// <summary>ADM-15: the address bots call.</summary>
    public string BotApiUrl { get; set; } = "";
}
