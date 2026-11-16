using System.Text.Encodings.Web;
using System.Text.Json;
using Dalan.Relay.Chats;
using Dalan.Relay.Queue;
using Microsoft.AspNetCore.Http;

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

internal sealed record SendMessageRequest(long? UserId, string? Text);

internal sealed record SentMessageJson(long Id, long UserId, string Text, DateTimeOffset Date)
{
    public static SentMessageJson Of(Message message) =>
        new(message.Seq, message.UserId, message.Content.Text, message.CreatedAt);
}

/// <summary>REL-11: every error has a stable code and a message that says how to fix it.</summary>
internal sealed record ErrorJson(string Error, string Message);

internal static class BotApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        // Persian text stays readable and short in the JSON (the default would write \uXXXX for every letter).
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static IResult Ok<T>(T body) => Results.Json(body, Options);

    public static IResult Error(int status, string code, string message) =>
        Results.Json(new ErrorJson(code, message), Options, statusCode: status);

    public static IResult BadRequest(string message) => Error(StatusCodes.Status400BadRequest, "bad_request", message);
}
