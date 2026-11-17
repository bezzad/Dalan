using System.Text.Json.Serialization;
using Dalan.Relay.Chats;
using Dalan.Relay.Queue;

namespace Dalan.Relay.BotApi;

internal sealed record UpdatesResponse(IEnumerable<UpdateJson> Updates);

internal sealed record UpdateJson(long UpdateId, IncomingMessageJson Message)
{
    public static UpdateJson Of(QueuedUpdate update) => new(
        update.Id,
        new IncomingMessageJson(
            update.Seq,
            new SenderJson(update.From.Id, update.From.DisplayName, update.From.Username),
            update.Text,
            update.Date));
}

internal sealed record IncomingMessageJson(long Id, SenderJson From, string Text, DateTimeOffset Date);

/// <summary>What a bot learns about a user. No phone number (REL-10).</summary>
internal sealed record SenderJson(long Id, string Name, string? Username);

internal sealed record SendMessageRequest(long? UserId, string? Text, List<List<string>>? ReplyKeyboard);

internal sealed record SentMessageJson(
    long Id,
    long UserId,
    string Text,
    DateTimeOffset Date,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<List<string>>? ReplyKeyboard)
{
    public static SentMessageJson Of(Message message) =>
        new(message.Seq, message.UserId, message.Content.Text, message.CreatedAt, message.Content.ReplyKeyboard?.ToRows());
}
