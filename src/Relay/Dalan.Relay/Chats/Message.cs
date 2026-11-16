namespace Dalan.Relay.Chats;

public enum Direction
{
    FromUser = 0,
    FromBot = 1,
}

/// <summary>Whether a user's message reached the bot.</summary>
public enum DeliveryState
{
    Pending = 0,
    Delivered = 1,
    NotDelivered = 2,
}

public sealed class Message
{
    /// <summary>For a user message this is also the update id the bot sees.</summary>
    public long Id { get; private set; }
    public long BotId { get; set; }
    public long UserId { get; set; }

    /// <summary>Sequence number inside the chat, starting at 1.</summary>
    public long Seq { get; set; }
    public Direction Direction { get; set; }
    public required MessageContent Content { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Set only for user messages; null for bot messages.</summary>
    public DeliveryState? Delivery { get; set; }
}

/// <summary>Stored as jsonb, so later milestones can add buttons and files without a schema change.</summary>
public sealed class MessageContent
{
    public required string Text { get; set; }
}
