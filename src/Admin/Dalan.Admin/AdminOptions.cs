namespace Dalan.Admin;

/// <summary>Section "Admin" of the settings. Starting values are in appsettings.json; nothing here is a default.</summary>
public sealed class AdminOptions
{
    public int AccessLogIntervalMinutes { get; set; }
    public int AccessLogRetentionDays { get; set; }
    public int AccessLogSweepMinutes { get; set; }
    public int PageSize { get; set; }
    public int MaxSettingLength { get; set; }

    /// <summary>ADM-17: the longest contact message, in characters.</summary>
    public int ContactMaxLength { get; set; }

    /// <summary>ADM-17: contact messages one address may send in 24 hours.</summary>
    public int ContactPerAddressPerDay { get; set; }

    /// <summary>ADM-21: the folder of the backups (deploy/backup.sh); a relative path is under the program's folder.</summary>
    public string BackupFolder { get; set; } = "";

    /// <summary>ADM-22: the largest backup the admin may upload, in MB.</summary>
    public int BackupMaxUploadMb { get; set; }

    /// <summary>ADM-15: the address of the site, for llms.txt and the ready prompt.</summary>
    public string SiteUrl { get; set; } = "";

    /// <summary>ADM-15: the address bots call.</summary>
    public string BotApiUrl { get; set; } = "";
}
