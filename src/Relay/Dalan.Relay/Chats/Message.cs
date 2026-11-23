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
        Content.ReplyKeyboard?.ToRows(), Content.InlineKeyboard?.ToRows(), Content.ReplyKeyboard?.ToOptions());
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

/// <summary>
/// Buttons under the user's input. A tap sends the button's text as a message.
/// Old rows hold only the texts; the styles and flags (REL-17) are new and read as "none" and false.
/// </summary>
public sealed class ReplyKeyboard
{
    public List<KeyboardRow> Rows { get; set; } = [];
    public bool Persistent { get; set; }
    public bool Resize { get; set; }
    public bool OneTime { get; set; }

    public static ReplyKeyboard? Of(List<List<string>>? rows, KeyboardOptions? options = null) =>
        rows is null ? null : new ReplyKeyboard
        {
            Rows = rows.ConvertAll((row) => new KeyboardRow { Buttons = row }),
            Persistent = options?.Persistent ?? false,
            Resize = options?.Resize ?? false,
            OneTime = options?.OneTime ?? false,
        }.WithStyles(options?.Styles);

    public List<List<string>> ToRows() => Rows.ConvertAll(row => row.Buttons);

    /// <summary>Null when no flag and no style is set, so the JSON stays as small as before.</summary>
    public KeyboardOptions? ToOptions()
    {
        var styles = Rows.Any(row => row.Styles is not null) ? Rows.ConvertAll(row => row.Styles ?? row.Buttons.ConvertAll(_ => (string?)null)) : null;
        return Persistent || Resize || OneTime || styles is not null ? new KeyboardOptions(Persistent, Resize, OneTime, styles) : null;
    }

    private ReplyKeyboard WithStyles(List<List<string?>>? styles)
    {
        if (styles is null) return this;
        for (var i = 0; i < Rows.Count && i < styles.Count; i++)
            Rows[i].Styles = styles[i]?.Exists(style => style is not null) == true ? styles[i] : null;
        return this;
    }
}

public sealed class KeyboardRow
{
    public List<string> Buttons { get; set; } = [];

    /// <summary>One style or null per button; null when no button of the row has a style.</summary>
    public List<string?>? Styles { get; set; }
}

public sealed class InlineKeyboard
{
    public List<InlineRow> Rows { get; set; } = [];

    public static InlineKeyboard? Of(List<List<InlineButton>>? rows) => rows is null ? null : new InlineKeyboard
    {
        Rows = rows.ConvertAll(row => new InlineRow
        {
            Buttons = row.ConvertAll(b => new InlineKeyboardButton { Text = b.Text, Data = b.CallbackData, Url = b.Url, Style = b.Style }),
        }),
    };

    public List<List<InlineButton>> ToRows() =>
        Rows.ConvertAll(row => row.Buttons.ConvertAll(b => new InlineButton(b.Text, b.Data, b.Url, b.Style)));
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
    public string? Style { get; set; }
}

public sealed class MessageFile
{
    public required string Id { get; set; }
    public FileKind Kind { get; set; }
    public required string Name { get; set; }
    public required string ContentType { get; set; }
    public long Size { get; set; }

    /// <summary>REL-50: seconds of a voice, audio or video; null when unknown.</summary>
    public int? Duration { get; set; }

    public static MessageFile Of(BotFile file) =>
        new() { Id = file.Id, Kind = file.Kind, Name = file.Name, ContentType = file.ContentType, Size = file.Size, Duration = file.Duration };

    public BotFile ToBot() => new(Id, Kind, Name, ContentType, Size, Duration);
}
