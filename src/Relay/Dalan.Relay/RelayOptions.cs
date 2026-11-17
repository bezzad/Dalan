namespace Dalan.Relay;

/// <summary>
/// Every Relay limit, section "Relay" of the settings. Starting values are in appsettings.json
/// (docs/DESIGN.md section 18); nothing here is a default.
/// </summary>
public sealed class RelayOptions
{
    public int UpdateExpirySeconds { get; set; }
    public int ExpirySweepSeconds { get; set; }
    public bool LatestCommandWins { get; set; }
    public int PollTimeoutDefaultSeconds { get; set; }
    public int PollTimeoutMaxSeconds { get; set; }
    public int MaxUpdatesPerPoll { get; set; }
    public int MaxTextLength { get; set; }
    public int RateLimitWindowSeconds { get; set; }
    public int BotRequestLimit { get; set; }
    public int UserRequestLimit { get; set; }
    public int MaxMessagesPerSync { get; set; }
    public int MaxKeyboardButtons { get; set; }
    public int MaxButtonTextLength { get; set; }
    public int MaxStreamsPerUser { get; set; }
    public int StreamHeartbeatSeconds { get; set; }
    public int StreamBufferSize { get; set; }
    public int OutboxPollSeconds { get; set; }
    public int OutboxBatchSize { get; set; }
    public int ClientReconnectMinSeconds { get; set; }
    public int ClientReconnectMaxSeconds { get; set; }
    public int MaxCaptionLength { get; set; }
    public int MaxCallbackDataBytes { get; set; }
    public int MaxButtonUrlLength { get; set; }
    public int CallbackAnswerSeconds { get; set; }
    public int MaxCallbackAnswerLength { get; set; }
    public int MaxCommands { get; set; }
    public int MaxCommandDescriptionLength { get; set; }

    /// <summary>Folder of the files; a relative path is under the program's folder.</summary>
    public string FileFolder { get; set; } = "";
    public int MaxFileSizeMb { get; set; }
    public int FileRetentionDays { get; set; }
    public int FileSweepMinutes { get; set; }
    public int UserUploadMbPerDay { get; set; }
    public int BotOnlineSeconds { get; set; }
    public int ClientStatusRefreshSeconds { get; set; }
    public int MaxBotsPerBuilder { get; set; }
    public int MaxBotNameLength { get; set; }

    public TimeSpan UpdateExpiry => TimeSpan.FromSeconds(UpdateExpirySeconds);
}
