namespace Dalan.Relay.Contracts;

public sealed record BotInfo(long Id, string Username, string Name);

/// <summary>
/// The answer to "may this call go on?". <see cref="Bot"/> is set when it may.
/// Otherwise <see cref="RetryAfter"/> is set when the bot is over its limit, and both are null when the token is wrong.
/// </summary>
public sealed record BotAccess(BotInfo? Bot, TimeSpan? RetryAfter);

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
public sealed record BotFile(string Id, FileKind Kind, string Name, string ContentType, long Size);

/// <summary>A button under a message (REL-20). Exactly one of <see cref="CallbackData"/> and <see cref="Url"/> is set.</summary>
public sealed record InlineButton(string Text, string? CallbackData = null, string? Url = null);

/// <summary>A message of a chat, from the user or from the bot. <see cref="Id"/> is the sequence number in the chat.</summary>
public sealed record BotMessage(
    long Id,
    long UserId,
    bool FromBot,
    string Text,
    DateTimeOffset Date,
    DateTimeOffset? EditDate = null,
    BotFile? File = null,
    List<List<string>>? ReplyKeyboard = null,
    List<List<InlineButton>>? InlineKeyboard = null);

/// <summary>A press on an inline button (REL-21). <see cref="Message"/> is the bot message that has the button.</summary>
public sealed record BotCallback(string Id, BotMessage Message, string Data);

/// <summary>One update for a bot: a user's message or a button press.</summary>
public sealed record BotUpdate(long Id, BotUser From, BotMessage? Message, BotCallback? Callback);

/// <summary>
/// A file to send: an upload (<see cref="Content"/>, <see cref="Name"/>, <see cref="ContentType"/>),
/// or a file the bot already has (<see cref="FileId"/>).
/// </summary>
public sealed record NewFile(FileKind Kind, Stream? Content = null, string? Name = null, string? ContentType = null, string? FileId = null);

/// <summary>What a bot sends. With a file, <see cref="Text"/> is the caption and may be empty.</summary>
public sealed record OutgoingMessage(
    string Text,
    List<List<string>>? ReplyKeyboard = null,
    List<List<InlineButton>>? InlineKeyboard = null,
    NewFile? File = null);

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
    NotModified,
    FileTooBig,
    FileNotFound,
    CallbackNotFound,
    BadCommands,
}

public sealed record BotResult<T>(BotError Error, T? Value);
