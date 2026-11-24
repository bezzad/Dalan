namespace Dalan.Relay.Contracts;

public sealed record BotInfo(long Id, string Username, string Name);

/// <summary>
/// The answer to "may this call go on?". <see cref="Bot"/> is set when it may.
/// Otherwise <see cref="Suspended"/> is true when an admin suspended the bot (REL-38), <see cref="RetryAfter"/>
/// is set when the bot is over its limit, and all are empty when the token is wrong.
/// </summary>
public sealed record BotAccess(BotInfo? Bot, TimeSpan? RetryAfter, bool Suspended = false);

/// <summary>What a bot learns about a user. No phone number (REL-10).</summary>
public sealed record BotUser(long Id, string Name, string? Username);

public enum FileKind
{
    Photo,
    Document,
    Audio,
    Voice,
    Video,
}

/// <summary>A stored file (REL-27, REL-28). <see cref="ContentType"/> is its MIME type.</summary>
public sealed record BotFile(string Id, FileKind Kind, string Name, string ContentType, long Size, int? Duration = null);

/// <summary>
/// A button under a message (REL-20). Exactly one of <see cref="CallbackData"/>, <see cref="Url"/> and <see cref="SwitchQuery"/> is set.
/// <see cref="SwitchQuery"/> (may be empty) is put in the user's input after "@username ".
/// </summary>
public sealed record InlineButton(string Text, string? CallbackData = null, string? Url = null, string? Style = null, string? SwitchQuery = null);

/// <summary>
/// REL-53: a part of a text drawn in a special way, as in Telegram. <see cref="Offset"/> and <see cref="Length"/> count UTF-16 code units.
/// <see cref="Url"/> is for text_link, <see cref="Language"/> for pre.
/// </summary>
public sealed record TextEntity(string Type, int Offset, int Length, string? Url = null, string? Language = null);

/// <summary>
/// REL-17: how the user's client shows a reply keyboard. <see cref="Styles"/> is one style (success, danger, primary) or null
/// per button, in the shape of the rows; null when no button has a style.
/// </summary>
public sealed record KeyboardOptions(bool Persistent = false, bool Resize = false, bool OneTime = false, List<List<string?>>? Styles = null);

/// <summary>
/// A message of a chat, from the user or from the bot. <see cref="Id"/> is the sequence number in the chat.
/// <see cref="ReplyTo"/> (REL-52) is a copy of the message it replies to, as it was then; that copy has no ReplyTo of its own.
/// </summary>
public sealed record BotMessage(
    long Id,
    long UserId,
    bool FromBot,
    string Text,
    DateTimeOffset Date,
    DateTimeOffset? EditDate = null,
    BotFile? File = null,
    List<List<string>>? ReplyKeyboard = null,
    List<List<InlineButton>>? InlineKeyboard = null,
    KeyboardOptions? ReplyOptions = null,
    List<TextEntity>? Entities = null,
    BotMessage? ReplyTo = null);

/// <summary>A press on an inline button (REL-21). <see cref="Message"/> is the bot message that has the button.</summary>
public sealed record BotCallback(string Id, BotMessage Message, string Data);

/// <summary>One update for a bot: a user's message or a button press.</summary>
public sealed record BotUpdate(long Id, BotUser From, BotMessage? Message, BotCallback? Callback);

/// <summary>
/// A file to send: an upload (<see cref="Content"/>, <see cref="Name"/>, <see cref="ContentType"/>),
/// or a file the bot already has (<see cref="FileId"/>).
/// </summary>
public sealed record NewFile(FileKind Kind, Stream? Content = null, string? Name = null, string? ContentType = null, string? FileId = null, int? Duration = null);

/// <summary>
/// What a bot sends. With a file, <see cref="Text"/> is the caption and may be empty.
/// <see cref="ReplyTo"/> (REL-52): the message it replies to; when that is gone, <see cref="AllowWithoutReply"/> sends it without the reply.
/// </summary>
public sealed record OutgoingMessage(
    string Text,
    List<List<string>>? ReplyKeyboard = null,
    List<List<InlineButton>>? InlineKeyboard = null,
    NewFile? File = null,
    KeyboardOptions? ReplyOptions = null,
    List<TextEntity>? Entities = null,
    long? ReplyTo = null,
    bool AllowWithoutReply = false);

/// <summary>Which part of a message an edit changes (REL-23).</summary>
public enum EditPart
{
    /// <summary>The text, or the caption of a file message.</summary>
    Any,

    /// <summary>Only the text: a file message gives <see cref="BotError.NoText"/>.</summary>
    Text,

    /// <summary>Only the caption: a text message gives <see cref="BotError.NoCaption"/>.</summary>
    Caption,
}

/// <summary>REL-23: null <see cref="Text"/> keeps the text and its entities; the inline keyboard is always replaced (null = none).</summary>
public sealed record MessageEdit(
    string? Text, List<TextEntity>? Entities, List<List<InlineButton>>? InlineKeyboard, EditPart Part = EditPart.Any);

public sealed record BotCommand(string Command, string Description);

public enum BotError
{
    None,
    TextEmpty,
    TextTooLong,
    BadKeyboard,
    ChatNotActive,
    MessageNotFound,
    NotEditable,
    NoText,
    NoCaption,
    NotModified,
    ReplyNotFound,
    BadEntities,
    FileTooBig,
    FileNotFound,
    CallbackNotFound,
    BadCommands,
    NotAnImage,
    ImageTooBig,
}

public sealed record BotResult<T>(BotError Error, T? Value);
