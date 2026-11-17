using System.Text.Json.Serialization;

namespace Dalan.Relay.ClientApi;

/// <summary>REL-14: a bot in the user's list, with the unread count (REL-32) and the status (REL-33).</summary>
internal sealed record BotJson(long Id, string Username, string Name, int Unread, bool Online, DateTimeOffset? LastSeen, bool Suspended);

internal sealed record BotsResponse(IEnumerable<BotJson> Bots);

internal sealed record FoundBotJson(long Id, string Username, string Name, bool Started, bool Suspended);

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

/// <summary>The data of a "read" event in the live stream (LIVE-10).</summary>
internal sealed record ReadEventJson(long BotId, int Unread);

internal sealed record ReadRequest(long? MessageId);

internal sealed record UnreadJson(int Unread);

internal sealed record StatusJson(bool Online, DateTimeOffset? LastSeen);

internal sealed record PressRequest(long? MessageId, string? Data);

internal sealed record PressJson(string Id);

internal sealed record CommandsJson(IEnumerable<CommandJson> Commands);

internal sealed record CommandJson(string Command, string Description);

/// <summary>REL-18: what the client must know and may not hard-code.</summary>
internal sealed record ClientSettingsJson(
    int MaxTextLength, int MaxCaptionLength, int MaxFileSizeMb, int SyncPageSize, int ReconnectMinSeconds, int ReconnectMaxSeconds,
    int StatusRefreshSeconds);
