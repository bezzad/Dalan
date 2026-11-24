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

    /// <summary>For reading JSON parameters (snake_case names).</summary>
    public static JsonSerializerOptions ReadOptions => Options;

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

/// <summary>Telegram's Update: a user's message, or a press on an inline button (TG-14).</summary>
internal sealed record UpdateJson(long UpdateId, MessageJson? Message, CallbackQueryJson? CallbackQuery)
{
    public static UpdateJson Of(BotInfo bot, BotUpdate update)
    {
        var from = UserJson.Of(update.From);
        return new UpdateJson(
            update.Id,
            update.Message is { } message ? MessageJson.Received(bot, update.From, message) : null,
            update.Callback is { } callback
                ? new CallbackQueryJson(
                    callback.Id, from, MessageJson.Sent(bot, callback.Message, update.From),
                    update.From.Id.ToString(CultureInfo.InvariantCulture), callback.Data)
                : null);
    }
}

internal sealed record CallbackQueryJson(string Id, UserJson From, MessageJson Message, string ChatInstance, string Data);

/// <summary>
/// Telegram's Message. <see cref="Date"/> is Unix seconds. A message with a file has a caption instead of a text,
/// and the file in the field of its kind (TG-20, TG-21).
/// </summary>
internal sealed record MessageJson(
    long MessageId,
    UserJson From,
    ChatJson Chat,
    long Date,
    long? EditDate,
    string? Text,
    IReadOnlyList<EntityJson>? Entities,
    IReadOnlyList<FileJson>? Photo,
    FileJson? Document,
    FileJson? Audio,
    FileJson? Voice,
    FileJson? Video,
    string? Caption,
    IReadOnlyList<EntityJson>? CaptionEntities,
    InlineMarkupJson? ReplyMarkup,
    MessageJson? ReplyToMessage)
{
    /// <summary>A user's message: its entities are the commands in it (TG-06).</summary>
    public static MessageJson Received(BotInfo bot, BotUser user, BotMessage message)
    {
        var chat = new ChatJson(user.Id, ChatJson.Private, user.Name, user.Username);
        return Of(message, UserJson.Of(user), chat, message.File is null ? BotCommands.Find(message.Text) : null, Reply(bot, user, chat, message));
    }

    /// <summary>
    /// A bot's message. <paramref name="user"/> is the user of the chat when known; a reply to the user's message
    /// then names them (TG-24), else its first_name is empty.
    /// </summary>
    public static MessageJson Sent(BotInfo bot, BotMessage message, BotUser? user = null)
    {
        var chat = new ChatJson(message.UserId, ChatJson.Private);
        return Of(message, BotUser(bot), chat, EntityJson.Of(message.Entities), Reply(bot, user ?? new BotUser(message.UserId, "", null), chat, message));
    }

    private static UserJson BotUser(BotInfo bot) => new(bot.Id, true, bot.Name, bot.Username);

    /// <summary>TG-24: the copy of the message replied to, without a reply of its own.</summary>
    private static MessageJson? Reply(BotInfo bot, BotUser user, ChatJson chat, BotMessage message) =>
        message.ReplyTo is { } target
            ? Of(target, target.FromBot ? BotUser(bot) : UserJson.Of(user), chat,
                target.FromBot ? EntityJson.Of(target.Entities) : target.File is null ? BotCommands.Find(target.Text) : null, null)
            : null;

    private static MessageJson Of(BotMessage message, UserJson from, ChatJson chat, IReadOnlyList<EntityJson>? entities, MessageJson? reply)
    {
        var file = message.File;
        FileJson? Of(FileKind kind) => file?.Kind == kind ? FileJson.Of(file) : null;
        var caption = file is not null && message.Text.Length > 0 ? message.Text : null;
        return new MessageJson(
            message.Id,
            from,
            chat,
            message.Date.ToUnixTimeSeconds(),
            message.EditDate?.ToUnixTimeSeconds(),
            file is null ? message.Text : null,
            file is null ? entities : null,
            Of(FileKind.Photo) is { } photo ? [photo] : null,
            Of(FileKind.Document),
            Of(FileKind.Audio),
            Of(FileKind.Voice),
            Of(FileKind.Video),
            caption,
            caption is not null ? entities : null,
            message.InlineKeyboard is { } rows ? InlineMarkupJson.Of(rows) : null,
            reply);
    }
}

/// <summary>
/// Telegram's PhotoSize, Document, Audio, Voice and Video in one shape; fields a kind does not have stay out.
/// Dalan does not read media, so width and height are 0; duration is the stored length (REL-50), else 0.
/// </summary>
internal sealed record FileJson(
    string FileId, string FileUniqueId, long FileSize, string? FileName, string? MimeType, int? Width, int? Height, int? Duration)
{
    public static FileJson Of(BotFile file) => file.Kind switch
    {
        FileKind.Photo => new(file.Id, file.Id, file.Size, null, null, 0, 0, null),
        FileKind.Voice => new(file.Id, file.Id, file.Size, null, file.ContentType, null, null, file.Duration ?? 0),
        FileKind.Audio => new(file.Id, file.Id, file.Size, file.Name, file.ContentType, null, null, file.Duration ?? 0),
        FileKind.Video => new(file.Id, file.Id, file.Size, file.Name, file.ContentType, 0, 0, file.Duration ?? 0),
        _ => new(file.Id, file.Id, file.Size, file.Name, file.ContentType, null, null, null),
    };
}

/// <summary>Telegram's File, the answer of getFile (TG-21). The path is the file id.</summary>
internal sealed record TelegramFileJson(string FileId, string FileUniqueId, long FileSize, string FilePath);

internal sealed record InlineMarkupJson(List<List<InlineButtonJson>> InlineKeyboard)
{
    public static InlineMarkupJson Of(List<List<InlineButton>> rows) =>
        new(rows.ConvertAll(row => row.ConvertAll(b => new InlineButtonJson(b.Text, b.CallbackData, b.Url, b.Style, b.SwitchQuery))));
}

/// <summary>TG-14: a switch button comes back as switch_inline_query_current_chat, what it does in Dalan.</summary>
internal sealed record InlineButtonJson(string Text, string? CallbackData, string? Url, string? Style = null, string? SwitchInlineQueryCurrentChat = null);

internal sealed record CommandJson(string Command, string Description);

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

    public static UserJson Of(BotUser user) => new(user.Id, false, user.Name, user.Username);
}

/// <summary>Telegram's Chat. A Dalan chat is always one user, so its id is the user's id.</summary>
internal sealed record ChatJson(long Id, string Type, string? FirstName = null, string? Username = null)
{
    public const string Private = "private";
}

/// <summary>Telegram's MessageEntity (TG-06, TG-25). <see cref="Url"/> is for text_link, <see cref="Language"/> for pre.</summary>
internal sealed record EntityJson(string Type, int Offset, int Length, string? Url = null, string? Language = null)
{
    public static List<EntityJson>? Of(List<TextEntity>? entities) =>
        entities is { Count: > 0 } ? entities.ConvertAll(e => new EntityJson(e.Type, e.Offset, e.Length, e.Url, e.Language)) : null;
}
