using Dalan.Relay.Contracts;

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

    /// <summary>REL-16: the chat's change counter at the last change of this message. Sync asks for versions above a number.</summary>
    public long Version { get; set; }
    public Direction Direction { get; set; }
    public required MessageContent Content { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? EditedAt { get; set; }

    /// <summary>REL-25: at most one message of a chat is pinned.</summary>
    public bool Pinned { get; set; }

    /// <summary>REL-24: a deleted message keeps its row with no content, so a syncing client learns to remove it.</summary>
    public bool Deleted { get; set; }

    /// <summary>Set only for user messages; null for bot messages.</summary>
    public DeliveryState? Delivery { get; set; }

    public BotMessage ToBot() => new(
        Seq, UserId, Direction == Direction.FromBot, Content.Text, CreatedAt, EditedAt, Content.File?.ToBot(),
        Content.ReplyKeyboard?.ToRows(), Content.InlineKeyboard?.ToRows());
}

/// <summary>Stored as jsonb.</summary>
public sealed class MessageContent
{
    /// <summary>The text, or the caption of a file (may then be empty).</summary>
    public required string Text { get; set; }

    /// <summary>REL-17: null = this message does not change the keyboard; no rows = remove it.</summary>
    public ReplyKeyboard? ReplyKeyboard { get; set; }

    /// <summary>REL-20: buttons under this message.</summary>
    public InlineKeyboard? InlineKeyboard { get; set; }

    /// <summary>A copy of the file's data (REL-27, REL-28). The bytes are on disk under <see cref="MessageFile.Id"/>.</summary>
    public MessageFile? File { get; set; }
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

public sealed class InlineKeyboard
{
    public List<InlineRow> Rows { get; set; } = [];

    public static InlineKeyboard? Of(List<List<InlineButton>>? rows) => rows is null ? null : new InlineKeyboard
    {
        Rows = rows.ConvertAll(row => new InlineRow
        {
            Buttons = row.ConvertAll(b => new InlineKeyboardButton { Text = b.Text, Data = b.CallbackData, Url = b.Url }),
        }),
    };

    public List<List<InlineButton>> ToRows() =>
        Rows.ConvertAll(row => row.Buttons.ConvertAll(b => new InlineButton(b.Text, b.Data, b.Url)));
}

public sealed class InlineRow
{
    public List<InlineKeyboardButton> Buttons { get; set; } = [];
}

public sealed class InlineKeyboardButton
{
    public string Text { get; set; } = "";
    public string? Data { get; set; }
    public string? Url { get; set; }
}

public sealed class MessageFile
{
    public required string Id { get; set; }
    public FileKind Kind { get; set; }
    public required string Name { get; set; }
    public required string ContentType { get; set; }
    public long Size { get; set; }

    public static MessageFile Of(BotFile file) =>
        new() { Id = file.Id, Kind = file.Kind, Name = file.Name, ContentType = file.ContentType, Size = file.Size };

    public BotFile ToBot() => new(Id, Kind, Name, ContentType, Size);
}
