using System.Text.Json.Serialization;
using Dalan.Relay.Bots;

namespace Dalan.Relay.ClientApi;

internal sealed record BotJson(long Id, string Username, string Name)
{
    public static BotJson Of(Bot bot) => new(bot.Id, bot.Username, bot.Name);
}

internal sealed record BotsResponse(IEnumerable<BotJson> Bots);

internal sealed record FoundBotJson(long Id, string Username, string Name, bool Started);

internal sealed record UserMessageRequest(string? Text);

internal sealed record MessagesResponse(IEnumerable<ChatMessageJson> Messages, ChatMessageJson? Pinned);

/// <summary>The data of a "message" event in the live stream (LIVE-02).</summary>
internal sealed record MessageEventJson(long BotId, ChatMessageJson Message);

/// <summary>The data of an "answer" event in the live stream (LIVE-09).</summary>
internal sealed record AnswerEventJson(
    long BotId,
    string CallbackId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Text,
    bool ShowAlert);

internal sealed record PressRequest(long? MessageId, string? Data);

internal sealed record PressJson(string Id);

internal sealed record CommandsJson(IEnumerable<CommandJson> Commands);

internal sealed record CommandJson(string Command, string Description);

/// <summary>REL-18: what the client must know and may not hard-code.</summary>
internal sealed record ClientSettingsJson(
    int MaxTextLength, int MaxCaptionLength, int MaxFileSizeMb, int SyncPageSize, int ReconnectMinSeconds, int ReconnectMaxSeconds);
