namespace Dalan.Relay.Contracts;

public sealed record BotInfo(long Id, string Username, string Name);

/// <summary>
/// The answer to "may this call go on?". <see cref="Bot"/> is set when it may.
/// Otherwise <see cref="RetryAfter"/> is set when the bot is over its limit, and both are null when the token is wrong.
/// </summary>
public sealed record BotAccess(BotInfo? Bot, TimeSpan? RetryAfter);

/// <summary>What a bot learns about a user. No phone number (REL-10).</summary>
public sealed record BotUser(long Id, string Name, string? Username);

/// <summary>One user message for a bot. <see cref="MessageId"/> is the sequence number in the chat.</summary>
public sealed record BotUpdate(long Id, long MessageId, BotUser From, string Text, DateTimeOffset Date);

/// <summary>A message the bot sent. <see cref="Id"/> is the sequence number in the chat.</summary>
public sealed record BotMessage(long Id, long UserId, string Text, DateTimeOffset Date, List<List<string>>? ReplyKeyboard);

public enum SendError
{
    None,
    TextEmpty,
    TextTooLong,
    BadKeyboard,
    ChatNotActive,
}

public sealed record SendResult(SendError Error, BotMessage? Message);
