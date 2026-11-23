namespace Dalan.Relay.Showcase;

public enum ShowcaseState
{
    Pending = 0,
    Listed = 1,
    Refused = 2,
}

/// <summary>REL-40: a builder asked to list this bot. A bot with no entry is listed nowhere.</summary>
public sealed class ShowcaseEntry
{
    public long BotId { get; set; }
    public required string Description { get; set; }
    public required string Category { get; set; }

    public ShowcaseState State { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
}
