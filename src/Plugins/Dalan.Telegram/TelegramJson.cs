using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dalan.Relay.Contracts;
using Microsoft.AspNetCore.Http;

namespace Dalan.Telegram;

/// <summary>TG-02: every answer is {"ok":true,"result":...} or {"ok":false,"error_code":...,"description":"..."}.</summary>
internal static class TelegramJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Persian text stays readable and short in the JSON.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static IResult Ok<T>(T result) => Results.Json(new OkJson<T>(true, result), Options);

    /// <summary>The HTTP status is the error code, as in Telegram.</summary>
    public static IResult Error(int status, string description, ErrorParametersJson? parameters = null) =>
        Results.Json(new ErrorJson(false, status, description, parameters), Options, statusCode: status);

    public static IResult BadRequest(string problem) => Error(StatusCodes.Status400BadRequest, $"Bad Request: {problem}");

    public static IResult BadBody() =>
        BadRequest("can't read the parameters; send them in the query string, as a form, or as a JSON object");

    /// <summary>TG-11.</summary>
    public static IResult TooManyRequests(HttpContext http, TimeSpan wait)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds));
        http.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        return Error(StatusCodes.Status429TooManyRequests,
            $"Too Many Requests: retry after {seconds}", new ErrorParametersJson(seconds));
    }
}

internal sealed record OkJson<T>(bool Ok, T Result);

internal sealed record ErrorJson(bool Ok, int ErrorCode, string Description, ErrorParametersJson? Parameters);

internal sealed record ErrorParametersJson(int RetryAfter);

/// <summary>Telegram's Update. Dalan has one kind for now: a message from a user.</summary>
internal sealed record UpdateJson(long UpdateId, MessageJson Message)
{
    public static UpdateJson Of(BotUpdate update) => new(update.Id, MessageJson.Received(update));
}

/// <summary>Telegram's Message. <see cref="Date"/> is Unix seconds.</summary>
internal sealed record MessageJson(
    long MessageId, UserJson From, ChatJson Chat, long Date, string Text, IReadOnlyList<EntityJson>? Entities)
{
    public static MessageJson Received(BotUpdate update) => new(
        update.MessageId,
        new UserJson(update.From.Id, false, update.From.Name, update.From.Username),
        new ChatJson(update.From.Id, ChatJson.Private, update.From.Name, update.From.Username),
        update.Date.ToUnixTimeSeconds(),
        update.Text,
        BotCommands.Find(update.Text));

    public static MessageJson Sent(BotInfo bot, BotMessage message) => new(
        message.Id,
        new UserJson(bot.Id, true, bot.Name, bot.Username),
        new ChatJson(message.UserId, ChatJson.Private),
        message.Date.ToUnixTimeSeconds(),
        message.Text,
        Entities: null);
}

/// <summary>Telegram's User: a person or a bot. No phone number (REL-10).</summary>
internal sealed record UserJson(
    long Id,
    bool IsBot,
    string FirstName,
    string? Username,
    bool? CanJoinGroups = null,
    bool? CanReadAllGroupMessages = null,
    bool? SupportsInlineQueries = null)
{
    /// <summary>TG-04: the answer of getMe. Dalan has no groups and no inline mode.</summary>
    public static UserJson Of(BotInfo bot) => new(bot.Id, true, bot.Name, bot.Username, false, false, false);
}

/// <summary>Telegram's Chat. A Dalan chat is always one user, so its id is the user's id.</summary>
internal sealed record ChatJson(long Id, string Type, string? FirstName = null, string? Username = null)
{
    public const string Private = "private";
}

internal sealed record EntityJson(string Type, int Offset, int Length);
