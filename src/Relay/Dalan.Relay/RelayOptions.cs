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

    public TimeSpan UpdateExpiry => TimeSpan.FromSeconds(UpdateExpirySeconds);
}
