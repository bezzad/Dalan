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

/// <summary>Stored as jsonb, so later milestones can add inline buttons and files without a schema change.</summary>
public sealed class MessageContent
{
    public required string Text { get; set; }

    /// <summary>REL-17: null = this message does not change the keyboard; no rows = remove it.</summary>
    public ReplyKeyboard? ReplyKeyboard { get; set; }
}

/// <summary>Buttons under the user's input. A tap sends the button's text as a message.</summary>
public sealed class ReplyKeyboard
{
    public List<KeyboardRow> Rows { get; set; } = [];

    public static ReplyKeyboard? Of(List<List<string>>? rows) =>
        rows is null ? null : new ReplyKeyboard { Rows = rows.ConvertAll(row => new KeyboardRow { Buttons = row }) };

    public List<List<string>> ToRows() => Rows.ConvertAll(row => row.Buttons);
}

public sealed class KeyboardRow
{
    public List<string> Buttons { get; set; } = [];
}
