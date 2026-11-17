using System.Text.Json;
using Dalan.Relay.ClientApi;
using Dalan.Relay.Contracts;
using Dalan.Relay.Files;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Dalan.Relay.BotApi;

/// <summary>The door for bots: Dalan's own Bot API, a thin HTTP layer over <see cref="IBotGateway"/>. The Telegram adapter is a separate plugin.</summary>
internal static class BotApiEndpoints
{
    public static void MapBotApi(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/v1").AddEndpointFilter<BotApiFilter>();
        api.MapGet("/updates", GetUpdatesAsync);
        api.MapPost("/messages", SendMessageAsync);
        api.MapMethods("/messages/{userId:long}/{messageId:long}", ["PATCH"], EditMessageAsync);
        api.MapDelete("/messages/{userId:long}/{messageId:long}", DeleteMessageAsync);
        api.MapPut("/chats/{userId:long}/pin", PinAsync);
        api.MapDelete("/chats/{userId:long}/pin", UnpinAsync);
        api.MapPost("/callbacks/{id}/answer", AnswerAsync);
        api.MapPut("/commands", SetCommandsAsync);
        api.MapGet("/commands", GetCommandsAsync);
        api.MapGet("/files/{fileId}", GetFileAsync);
    }

    private static async Task<IResult> GetUpdatesAsync(HttpContext http, IBotGateway gateway, IOptionsMonitor<RelayOptions> options)
    {
        if (PollRequest.Parse(http.Request.Query, options.CurrentValue, out var error) is not { } poll)
            return ApiJson.BadRequest(error);

        var updates = await gateway.GetUpdatesAsync(
            BotApiFilter.BotId(http), poll.Offset, poll.Limit, (long)poll.Timeout.TotalSeconds, http.RequestAborted);

        return updates is null
            ? ApiJson.Error(StatusCodes.Status409Conflict, "conflict",
                "A newer get-updates call with the same token replaced this one. Run only one copy of the bot.")
            : ApiJson.Ok(new UpdatesResponse(updates.Select(UpdateJson.Of)));
    }

    /// <summary>REL-08, REL-17, REL-20, REL-27: JSON, or multipart/form-data with the part "file".</summary>
    private static async Task<IResult> SendMessageAsync(
        HttpContext http, IBotGateway gateway, FileStore files, IOptionsMonitor<RelayOptions> options)
    {
        SendMessageRequest? request;
        IFormFile? upload = null;
        if (http.Request.HasFormContentType)
        {
            AllowFileUpload(http, files);
            var form = await http.Request.ReadFormAsync(http.RequestAborted);
            upload = form.Files["file"];
            request = ReadForm(form);
        }
        else
        {
            request = await ApiJson.ReadAsync<SendMessageRequest>(http);
        }

        if (request is not { UserId: { } userId })
        {
            return ApiJson.BadRequest(
                """Send JSON like {"user_id": 1, "text": "hello"}, or a multipart form with user_id, text and the part "file".""");
        }

        if (!TryKind(request.Kind, out var kind)) return ApiJson.BadRequest("'kind' must be photo, document, audio, voice or video.");

        await using var content = upload?.OpenReadStream();
        var file = content is not null ? new NewFile(kind, content, upload!.FileName, upload.ContentType)
            : request.FileId is { } fileId ? new NewFile(kind, FileId: fileId)
            : null;
        if (file is null && request.Text is null) return ApiJson.BadRequest("Send a 'text', a 'file' or a 'file_id'.");

        var (error, message) = await gateway.SendAsync(
            BotApiFilter.BotId(http),
            userId,
            new OutgoingMessage(request.Text ?? "", request.ReplyKeyboard, ToButtons(request.InlineKeyboard), file),
            http.RequestAborted);
        return error == BotError.None ? ApiJson.Ok(SentMessageJson.Of(message!)) : ErrorOf(error, options.CurrentValue);
    }

    /// <summary>REL-23.</summary>
    private static async Task<IResult> EditMessageAsync(
        long userId, long messageId, HttpContext http, IBotGateway gateway, IOptionsMonitor<RelayOptions> options)
    {
        if (await ApiJson.ReadAsync<EditMessageRequest>(http) is not { } request)
            return ApiJson.BadRequest("""Send JSON like {"text": "new text", "inline_keyboard": [...]}.""");

        var (error, message) = await gateway.EditAsync(
            BotApiFilter.BotId(http), userId, messageId, request.Text, ToButtons(request.InlineKeyboard), http.RequestAborted);
        return error == BotError.None ? ApiJson.Ok(SentMessageJson.Of(message!)) : ErrorOf(error, options.CurrentValue);
    }

    /// <summary>REL-24.</summary>
    private static async Task<IResult> DeleteMessageAsync(
        long userId, long messageId, HttpContext http, IBotGateway gateway, IOptionsMonitor<RelayOptions> options) =>
        Done(await gateway.DeleteAsync(BotApiFilter.BotId(http), userId, messageId, http.RequestAborted), options);

    /// <summary>REL-25.</summary>
    private static async Task<IResult> PinAsync(long userId, HttpContext http, IBotGateway gateway, IOptionsMonitor<RelayOptions> options)
    {
        if (await ApiJson.ReadAsync<PinRequest>(http) is not { MessageId: { } messageId })
            return ApiJson.BadRequest("""Send JSON like {"message_id": 2}.""");

        return Done(await gateway.PinAsync(BotApiFilter.BotId(http), userId, messageId, http.RequestAborted), options);
    }

    private static async Task<IResult> UnpinAsync(long userId, HttpContext http, IBotGateway gateway, IOptionsMonitor<RelayOptions> options) =>
        Done(await gateway.UnpinAsync(BotApiFilter.BotId(http), userId, http.RequestAborted), options);

    /// <summary>REL-22. An empty body is fine: the answer then has no text.</summary>
    private static async Task<IResult> AnswerAsync(string id, HttpContext http, IBotGateway gateway, IOptionsMonitor<RelayOptions> options)
    {
        var request = http.Request.ContentLength is null or 0 ? new AnswerRequest(null, null) : await ApiJson.ReadAsync<AnswerRequest>(http);
        if (request is null) return ApiJson.BadRequest("""Send JSON like {"text": "Saved", "show_alert": false}, or no body.""");

        return Done(await gateway.AnswerCallbackAsync(
            BotApiFilter.BotId(http), id, request.Text, request.ShowAlert ?? false, http.RequestAborted), options);
    }

    /// <summary>REL-26.</summary>
    private static async Task<IResult> SetCommandsAsync(HttpContext http, IBotGateway gateway, IOptionsMonitor<RelayOptions> options)
    {
        if (await ApiJson.ReadAsync<CommandsRequest>(http) is not { Commands: { } commands })
            return ApiJson.BadRequest("""Send JSON like {"commands": [{"command": "start", "description": "Start"}]}.""");

        return Done(await gateway.SetCommandsAsync(
            BotApiFilter.BotId(http), commands.ConvertAll(c => new BotCommand(c?.Command!, c?.Description!)), http.RequestAborted), options);
    }

    private static async Task<IResult> GetCommandsAsync(HttpContext http, IBotGateway gateway) =>
        ApiJson.Ok(new CommandsJson((await gateway.GetCommandsAsync(BotApiFilter.BotId(http), http.RequestAborted))
            .Select(c => new CommandJson(c.Command, c.Description))));

    /// <summary>REL-29.</summary>
    private static async Task<IResult> GetFileAsync(string fileId, HttpContext http, IBotGateway gateway)
    {
        var botId = BotApiFilter.BotId(http);
        if (await gateway.GetFileAsync(botId, fileId, http.RequestAborted) is { } file
            && await gateway.OpenFileAsync(botId, fileId, http.RequestAborted) is { } content)
        {
            return FileResponse.Of(http, file, content);
        }

        return FileNotFound();
    }

    /// <summary>The body may be as big as one file, plus a little for the other fields.</summary>
    public static void AllowFileUpload(HttpContext http, FileStore files)
    {
        if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
            limit.MaxRequestBodySize = files.MaxBytes + (1024 * 1024);
    }

    private static SendMessageRequest? ReadForm(IFormCollection form)
    {
        try
        {
            return new SendMessageRequest(
                long.TryParse(form["user_id"], out var userId) ? userId : null,
                form.TryGetValue("text", out var text) ? text.ToString() : null,
                Json<List<List<string>>>(form["reply_keyboard"]),
                Json<List<List<InlineButtonJson>>>(form["inline_keyboard"]),
                form.TryGetValue("file_id", out var fileId) ? fileId.ToString() : null,
                form.TryGetValue("kind", out var kind) ? kind.ToString() : null);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static T? Json<T>(string? value) where T : class =>
        string.IsNullOrEmpty(value) ? null : JsonSerializer.Deserialize<T>(value, ApiJson.Options);

    private static bool TryKind(string? text, out FileKind kind)
    {
        kind = FileKind.Document;
        return text is null || (Enum.TryParse(text, ignoreCase: true, out kind) && !int.TryParse(text, out _));
    }

    private static List<List<InlineButton>>? ToButtons(List<List<InlineButtonJson>>? rows) =>
        rows?.ConvertAll(row => row?.ConvertAll(button => button?.ToButton()!)!);

    private static IResult Done(BotError error, IOptionsMonitor<RelayOptions> options) =>
        error == BotError.None ? ApiJson.Ok(true) : ErrorOf(error, options.CurrentValue);

    private static IResult FileNotFound() => ApiJson.Error(StatusCodes.Status404NotFound, "file_not_found",
        "There is no file with this id for this bot, or it expired. A file is kept for a limited time.");

    /// <summary>REL-11: one stable code and a message that says how to fix it, for every refusal of the gateway.</summary>
    private static IResult ErrorOf(BotError error, RelayOptions options) => error switch
    {
        BotError.TextEmpty => ApiJson.Error(StatusCodes.Status400BadRequest, "text_empty",
            "'text' is empty. Send at least one character."),
        BotError.TextTooLong => ApiJson.Error(StatusCodes.Status400BadRequest, "text_too_long",
            $"The text is too long: at most {options.MaxTextLength} characters for a message, {options.MaxCaptionLength} " +
            $"for a file caption, {options.MaxCallbackAnswerLength} for a button answer."),
        BotError.BadKeyboard => ApiJson.Error(StatusCodes.Status400BadRequest, "bad_keyboard",
            $"A keyboard must be rows of buttons: at most {options.MaxKeyboardButtons} buttons, each text 1 to " +
            $"{options.MaxButtonTextLength} characters. An inline button needs exactly one of 'callback_data' " +
            $"(1 to {options.MaxCallbackDataBytes} bytes) or 'url' (http or https)."),
        BotError.MessageNotFound => ApiJson.Error(StatusCodes.Status404NotFound, "message_not_found",
            "This chat has no such message, or it was deleted."),
        BotError.NotEditable => ApiJson.Error(StatusCodes.Status400BadRequest, "not_editable",
            "A bot can edit only its own messages."),
        BotError.NoText => ApiJson.Error(StatusCodes.Status400BadRequest, "no_text",
            "This message carries a file; its caption cannot be edited. Send only 'inline_keyboard'."),
        BotError.NotModified => ApiJson.Error(StatusCodes.Status400BadRequest, "not_modified",
            "The new text and buttons are the same as the old ones."),
        BotError.FileTooBig => ApiJson.Error(StatusCodes.Status413PayloadTooLarge, "file_too_big",
            $"The file is bigger than {options.MaxFileSizeMb} MB."),
        BotError.FileNotFound => ApiJson.Error(StatusCodes.Status400BadRequest, "file_not_found",
            "This bot has no file with this 'file_id', or it expired. Upload the file again."),
        BotError.CallbackNotFound => ApiJson.Error(StatusCodes.Status400BadRequest, "callback_not_found",
            $"There is no button press with this id for this bot, or it is older than {options.CallbackAnswerSeconds} seconds."),
        BotError.BadCommands => ApiJson.Error(StatusCodes.Status400BadRequest, "bad_commands",
            $"At most {options.MaxCommands} commands; each 'command' is 1 to 32 of a-z, 0-9 and _, " +
            $"each 'description' 1 to {options.MaxCommandDescriptionLength} characters."),
        _ => ApiJson.Error(StatusCodes.Status403Forbidden, "chat_not_active",
            "This user has not started the bot, or has stopped it. A bot can write only to users who started it."),
    };
}
