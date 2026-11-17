using System.Text.Json;
using Dalan.Relay.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;

namespace Dalan.Telegram;

/// <summary>
/// Plugin that turns Telegram Bot API calls into Relay contract calls and back (rules TG-*).
/// It follows Telegram Bot API 10.4 (2026-11-09) and serves only what the core can do; see docs/ARCHITECTURE.md section 6.
/// </summary>
public static class TelegramAdapter
{
    public static IEndpointRouteBuilder MapTelegramAdapter(this IEndpointRouteBuilder endpoints)
    {
        // TG-01: the address of Telegram, at the root: some libraries keep only the host of their base address.
        // The token is part of the path, so nothing may log the path (TG-10).
        endpoints.MapMethods("/bot{token}/{method}", ["GET", "POST"], HandleAsync);
        // TG-21: Telegram's file download address.
        endpoints.MapGet("/file/bot{token}/{path}", DownloadAsync);
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(string token, string method, HttpContext http, IBotGateway gateway)
    {
        // ADM-01: the gateway records the call with the real address (CLAUDE.md rule 13: never read X-Forwarded-For here).
        var access = await gateway.EnterAsync(
            token, http.Connection.RemoteIpAddress, http.Connection.RemotePort, http.RequestAborted);
        if (access.RetryAfter is { } wait) return TelegramJson.TooManyRequests(http, wait);
        // TG-22.
        if (access.Suspended) return TelegramJson.Error(StatusCodes.Status403Forbidden, "Forbidden: bot was suspended");
        if (access.Bot is not { } bot) return TelegramJson.Error(StatusCodes.Status401Unauthorized, "Unauthorized");

        var name = method.ToUpperInvariant();
        if (name == "GETME") return TelegramJson.Ok(UserJson.Of(bot));

        // TG-20: a file upload may be as big as the core allows.
        if (name.StartsWith("SEND", StringComparison.Ordinal)
            && http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        {
            limit.MaxRequestBodySize = gateway.MaxUploadBytes + (1024 * 1024);
        }

        if (await TelegramParameters.ReadAsync(http) is not { } parameters) return TelegramJson.BadBody();

        var call = new Call(bot, parameters, gateway, http.RequestAborted);
        return name switch
        {
            "GETUPDATES" => await call.GetUpdatesAsync(),
            "SENDMESSAGE" => await call.SendAsync(null),
            "SENDPHOTO" => await call.SendAsync(FileKind.Photo),
            "SENDDOCUMENT" => await call.SendAsync(FileKind.Document),
            "SENDAUDIO" => await call.SendAsync(FileKind.Audio),
            "SENDVOICE" => await call.SendAsync(FileKind.Voice),
            "SENDVIDEO" => await call.SendAsync(FileKind.Video),
            "EDITMESSAGETEXT" => await call.EditAsync(withText: true),
            "EDITMESSAGEREPLYMARKUP" => await call.EditAsync(withText: false),
            "DELETEMESSAGE" => await call.ChangeAsync(Change.Delete),
            "PINCHATMESSAGE" => await call.ChangeAsync(Change.Pin),
            "UNPINCHATMESSAGE" or "UNPINALLCHATMESSAGES" => await call.ChangeAsync(Change.Unpin),
            "ANSWERCALLBACKQUERY" => await call.AnswerAsync(),
            "SETMYCOMMANDS" => await call.SetCommandsAsync(clear: false),
            "DELETEMYCOMMANDS" => await call.SetCommandsAsync(clear: true),
            "GETMYCOMMANDS" => await call.GetCommandsAsync(),
            "GETFILE" => await call.GetFileAsync(),
            // TG-09: Dalan has no webhook to delete, and shows no "typing" yet. Libraries call both and expect true.
            "DELETEWEBHOOK" or "SENDCHATACTION" => TelegramJson.Ok(true),
            _ => TelegramJson.BadRequest($"method {method} is not supported by Dalan"),
        };
    }

    /// <summary>TG-21: the bytes of a file, as Telegram serves them at /file/bot&lt;token&gt;/&lt;file_path&gt;.</summary>
    private static async Task<IResult> DownloadAsync(string token, string path, HttpContext http, IBotGateway gateway)
    {
        var access = await gateway.EnterAsync(
            token, http.Connection.RemoteIpAddress, http.Connection.RemotePort, http.RequestAborted);
        if (access.RetryAfter is { } wait) return TelegramJson.TooManyRequests(http, wait);
        // TG-22.
        if (access.Suspended) return TelegramJson.Error(StatusCodes.Status403Forbidden, "Forbidden: bot was suspended");
        if (access.Bot is not { } bot) return TelegramJson.Error(StatusCodes.Status401Unauthorized, "Unauthorized");

        if (await gateway.GetFileAsync(bot.Id, path, http.RequestAborted) is not { } file
            || await gateway.OpenFileAsync(bot.Id, path, http.RequestAborted) is not { } content)
        {
            return TelegramJson.Error(StatusCodes.Status404NotFound, "Not Found");
        }

        http.Response.Headers.ContentDisposition = "attachment";
        return Results.Stream(content, file.ContentType);
    }

    private enum Change
    {
        Delete,
        Pin,
        Unpin,
    }

    /// <summary>One adapter call of a bot whose token was checked.</summary>
    private sealed class Call(BotInfo bot, TelegramParameters parameters, IBotGateway gateway, CancellationToken cancellationToken)
    {
        // TG-21 and TG-20: the field of each send method.
        private static readonly Dictionary<FileKind, string> FileFields = new()
        {
            [FileKind.Photo] = "photo",
            [FileKind.Document] = "document",
            [FileKind.Audio] = "audio",
            [FileKind.Voice] = "voice",
            [FileKind.Video] = "video",
        };

        public async Task<IResult> GetUpdatesAsync()
        {
            // TG-05: the gateway cuts every value to its range, so a negative offset counts as 0.
            var updates = await gateway.GetUpdatesAsync(
                bot.Id, parameters.Number("offset") ?? 0, parameters.Number("limit"), parameters.Number("timeout"), cancellationToken);

            return updates is null
                ? TelegramJson.Error(StatusCodes.Status409Conflict,
                    "Conflict: terminated by other getUpdates request; make sure that only one bot instance is running")
                : TelegramJson.Ok(updates.Select(u => UpdateJson.Of(bot, u)));
        }

        /// <summary>TG-07 and TG-20: a text message, or a file of <paramref name="kind"/> with a caption.</summary>
        public async Task<IResult> SendAsync(FileKind? kind)
        {
            // A chat is one user, so chat_id is the user's id. A channel name can never be one.
            if (parameters.Number("chat_id") is not { } userId) return TelegramJson.BadRequest("chat not found");
            if (ReplyMarkup.Read(parameters, out var keyboard, out var inline) is { } problem) return TelegramJson.BadRequest(problem);

            NewFile? file = null;
            await using var content = kind is { } k ? parameters.File(FileFields[k])?.OpenReadStream() : null;
            if (kind is { } fileKind)
            {
                var field = FileFields[fileKind];
                if (parameters.File(field) is { } part) file = new NewFile(fileKind, content, part.FileName, part.ContentType);
                else if (parameters.Text(field) is not { Length: > 0 } fileId || fileId.StartsWith("attach://", StringComparison.Ordinal))
                    return TelegramJson.BadRequest($"there is no {field} in the request");
                else if (fileId.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || fileId.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    return TelegramJson.BadRequest("sending a file by URL is not supported by Dalan; upload it or send a file_id");
                else file = new NewFile(fileKind, FileId: fileId);
            }

            var text = (file is null ? parameters.Text("text") : parameters.Text("caption")) ?? "";
            var (error, message) = await gateway.SendAsync(bot.Id, userId, new OutgoingMessage(text, keyboard, inline, file), cancellationToken);
            return error == BotError.None ? TelegramJson.Ok(MessageJson.Sent(bot, message!)) : ErrorOf(error, "send", file is not null);
        }

        /// <summary>TG-16: editMessageText (<paramref name="withText"/>) and editMessageReplyMarkup.</summary>
        public async Task<IResult> EditAsync(bool withText)
        {
            if (parameters.Has("inline_message_id")) return TelegramJson.BadRequest("inline messages are not supported by Dalan");
            if (parameters.Number("chat_id") is not { } userId) return TelegramJson.BadRequest("chat not found");
            if (parameters.Number("message_id") is not { } messageId) return TelegramJson.BadRequest("message to edit not found");
            if (ReplyMarkup.ReadInline(parameters, out var inline) is { } problem) return TelegramJson.BadRequest(problem);

            var text = withText ? parameters.Text("text") ?? "" : null;
            var (error, message) = await gateway.EditAsync(bot.Id, userId, messageId, text, inline, cancellationToken);
            return error == BotError.None ? TelegramJson.Ok(MessageJson.Sent(bot, message!)) : ErrorOf(error, "edit", withFile: false);
        }

        /// <summary>TG-17 and TG-18.</summary>
        public async Task<IResult> ChangeAsync(Change change)
        {
            if (parameters.Number("chat_id") is not { } userId) return TelegramJson.BadRequest("chat not found");

            var messageId = parameters.Number("message_id");
            if (change != Change.Unpin && messageId is null)
                return TelegramJson.BadRequest(change == Change.Pin ? "message to pin not found" : "message to delete not found");

            var error = change switch
            {
                Change.Delete => await gateway.DeleteAsync(bot.Id, userId, messageId!.Value, cancellationToken),
                Change.Pin => await gateway.PinAsync(bot.Id, userId, messageId!.Value, cancellationToken),
                _ => await gateway.UnpinAsync(bot.Id, userId, cancellationToken),
            };
            return error == BotError.None
                ? TelegramJson.Ok(true)
                : ErrorOf(error, change == Change.Delete ? "delete" : "pin", withFile: false);
        }

        /// <summary>TG-15.</summary>
        public async Task<IResult> AnswerAsync()
        {
            if (parameters.Has("url")) return TelegramJson.BadRequest("url in answerCallbackQuery is not supported by Dalan");

            var error = await gateway.AnswerCallbackAsync(
                bot.Id, parameters.Text("callback_query_id") ?? "", parameters.Text("text"), parameters.Flag("show_alert"), cancellationToken);
            return error == BotError.None ? TelegramJson.Ok(true) : ErrorOf(error, "answer", withFile: false);
        }

        /// <summary>TG-19: Dalan has one list for everyone; a list for a language or a narrower scope is accepted and not kept.</summary>
        public async Task<IResult> SetCommandsAsync(bool clear)
        {
            if (!ForEveryone(out var scopeProblem)) return scopeProblem ?? TelegramJson.Ok(true);

            List<BotCommand> commands = [];
            if (!clear)
            {
                if (!parameters.TryArray("commands", out var given) || given is not { } list)
                    return TelegramJson.BadRequest("can't parse commands: they must be a JSON list");

                try
                {
                    commands = [.. list.Deserialize<List<CommandJson>>(TelegramJson.ReadOptions)!.Select(c => new BotCommand(c?.Command!, c?.Description!))];
                }
                catch (JsonException)
                {
                    return TelegramJson.BadRequest("can't parse commands: each one needs command and description");
                }
            }

            var error = await gateway.SetCommandsAsync(bot.Id, commands, cancellationToken);
            return error == BotError.None ? TelegramJson.Ok(true) : ErrorOf(error, "commands", withFile: false);
        }

        public async Task<IResult> GetCommandsAsync()
        {
            if (!ForEveryone(out var scopeProblem)) return scopeProblem ?? TelegramJson.Ok(Array.Empty<CommandJson>());

            var commands = await gateway.GetCommandsAsync(bot.Id, cancellationToken);
            return TelegramJson.Ok(commands.Select(c => new CommandJson(c.Command, c.Description)));
        }

        /// <summary>TG-21.</summary>
        public async Task<IResult> GetFileAsync()
        {
            var fileId = parameters.Text("file_id") ?? "";
            return await gateway.GetFileAsync(bot.Id, fileId, cancellationToken) is { } file
                ? TelegramJson.Ok(new TelegramFileJson(file.Id, file.Id, file.Size, file.Id))
                : TelegramJson.BadRequest("invalid file_id");
        }

        /// <summary>True when the call is for the one list Dalan keeps: no language and the default or all-private-chats scope.</summary>
        private bool ForEveryone(out IResult? problem)
        {
            problem = null;
            if (parameters.Text("language_code") is { Length: > 0 }) return false;
            if (!parameters.TryObject("scope", out var scope))
            {
                problem = TelegramJson.BadRequest("can't parse scope: it must be a JSON object");
                return false;
            }

            var type = scope is { } s && s.TryGetProperty("type", out var t) ? t.GetString() : "default";
            return type is "default" or "all_private_chats";
        }

        /// <summary>Telegram's own error texts, so bots that match on them keep working.</summary>
        private static IResult ErrorOf(BotError error, string action, bool withFile) => error switch
        {
            BotError.TextEmpty => TelegramJson.BadRequest("message text is empty"),
            BotError.TextTooLong when action == "answer" => TelegramJson.BadRequest("text is too long"),
            BotError.TextTooLong => TelegramJson.BadRequest(withFile ? "message caption is too long" : "message is too long"),
            BotError.BadKeyboard => TelegramJson.BadRequest(
                "the keyboard has too many buttons, a button text is empty or too long, " +
                "or an inline button has not exactly one of callback_data and an http(s) url"),
            BotError.MessageNotFound => TelegramJson.BadRequest($"message to {action} not found"),
            BotError.NotEditable => TelegramJson.BadRequest("message can't be edited"),
            BotError.NoText => TelegramJson.BadRequest("there is no text in the message to edit"),
            BotError.NotModified => TelegramJson.BadRequest(
                "message is not modified: specified new message content and reply markup are exactly the same as a current content and reply markup of the message"),
            BotError.FileTooBig => TelegramJson.Error(StatusCodes.Status413PayloadTooLarge, "Request Entity Too Large"),
            BotError.FileNotFound => TelegramJson.BadRequest("wrong file identifier/HTTP URL specified"),
            BotError.CallbackNotFound => TelegramJson.BadRequest("query is too old and response timeout expired or query ID is invalid"),
            BotError.BadCommands => TelegramJson.BadRequest("BOT_COMMAND_INVALID"),
            _ => TelegramJson.Error(StatusCodes.Status403Forbidden, "Forbidden: bot can't initiate conversation with a user"),
        };
    }
}
