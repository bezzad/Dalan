using System.Text.Json.Serialization;
using Dalan.Relay.Bots;
using Dalan.Relay.Chats;
using Dalan.Relay.Live;

namespace Dalan.Relay.ClientApi;

internal sealed record BotJson(long Id, string Username, string Name)
{
    public static BotJson Of(Bot bot) => new(bot.Id, bot.Username, bot.Name);
}

internal sealed record BotsResponse(IEnumerable<BotJson> Bots);

internal sealed record FoundBotJson(long Id, string Username, string Name, bool Started);

internal sealed record UserMessageRequest(string? Text);

/// <summary>A message as a person sees it. Its id is the sequence number in the chat.</summary>
internal sealed record ChatMessageJson(
    long Id,
    string From,
    string Text,
    DateTimeOffset Date,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<List<string>>? ReplyKeyboard)
{
    public static ChatMessageJson Of(Message message) => new(
        message.Seq, Name(message.Direction), message.Content.Text, message.CreatedAt, message.Content.ReplyKeyboard?.ToRows());

    public static ChatMessageJson Of(MessageCreated message) => new(
        message.Seq, Name(message.Direction), message.Text, message.Date, message.ReplyKeyboard);

    private static string Name(Direction direction) => direction == Direction.FromBot ? "bot" : "user";
}

internal sealed record MessagesResponse(IEnumerable<ChatMessageJson> Messages);

/// <summary>The data of a "message" event in the live stream (LIVE-02).</summary>
internal sealed record MessageEventJson(long BotId, ChatMessageJson Message);

/// <summary>REL-18: what the client must know and may not hard-code.</summary>
internal sealed record ClientSettingsJson(int MaxTextLength, int SyncPageSize, int ReconnectMinSeconds, int ReconnectMaxSeconds);
