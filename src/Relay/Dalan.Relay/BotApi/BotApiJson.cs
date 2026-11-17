using System.Text.Json.Serialization;
using Dalan.Relay.ClientApi;
using Dalan.Relay.Contracts;

namespace Dalan.Relay.BotApi;

internal sealed record UpdatesResponse(IEnumerable<UpdateJson> Updates);

/// <summary>REL-07: an update has either a message or a button press (REL-21).</summary>
internal sealed record UpdateJson(
    long UpdateId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IncomingMessageJson? Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CallbackJson? Callback)
{
    public static UpdateJson Of(BotUpdate update)
    {
        var from = new SenderJson(update.From.Id, update.From.Name, update.From.Username);
        return new UpdateJson(
            update.Id,
            update.Message is { } message
                ? new IncomingMessageJson(message.Id, from, message.Text, message.Date, message.File is { } file ? FileJson.Of(file) : null)
                : null,
            update.Callback is { } callback
                ? new CallbackJson(callback.Id, from, SentMessageJson.Of(callback.Message), callback.Data)
                : null);
    }
}

internal sealed record IncomingMessageJson(
    long Id,
    SenderJson From,
    string Text,
    DateTimeOffset Date,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] FileJson? File);

internal sealed record CallbackJson(string Id, SenderJson From, SentMessageJson Message, string Data);

/// <summary>What a bot learns about a user. No phone number (REL-10).</summary>
internal sealed record SenderJson(long Id, string Name, string? Username);

internal sealed record SendMessageRequest(
    long? UserId,
    string? Text,
    List<List<string>>? ReplyKeyboard,
    List<List<InlineButtonJson>>? InlineKeyboard,
    string? FileId,
    string? Kind);

internal sealed record EditMessageRequest(string? Text, List<List<InlineButtonJson>>? InlineKeyboard);

internal sealed record PinRequest(long? MessageId);

internal sealed record AnswerRequest(string? Text, bool? ShowAlert);

internal sealed record CommandsRequest(List<CommandJson>? Commands);

/// <summary>A message of a chat as the bot sees it (REL-08).</summary>
internal sealed record SentMessageJson(
    long Id,
    long UserId,
    string Text,
    DateTimeOffset Date,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<List<string>>? ReplyKeyboard,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<List<InlineButtonJson>>? InlineKeyboard,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] FileJson? File,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? Edited)
{
    public static SentMessageJson Of(BotMessage message) => new(
        message.Id,
        message.UserId,
        message.Text,
        message.Date,
        message.ReplyKeyboard,
        message.InlineKeyboard?.ConvertAll(row => row.ConvertAll(InlineButtonJson.Of)),
        message.File is { } file ? FileJson.Of(file) : null,
        message.EditDate);
}
