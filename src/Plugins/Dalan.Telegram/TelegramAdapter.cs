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
        // TG-23: so is the picture of setMyProfilePhoto.
        if ((name.StartsWith("SEND", StringComparison.Ordinal) || name == "SETMYPROFILEPHOTO")
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
            "EDITMESSAGETEXT" => await call.EditAsync(EditPart.Text),
            "EDITMESSAGECAPTION" => await call.EditAsync(EditPart.Caption),
            "EDITMESSAGEREPLYMARKUP" => await call.EditAsync(null),
            "DELETEMESSAGE" => await call.ChangeAsync(Change.Delete),
            "DELETEMESSAGES" => await call.DeleteManyAsync(),
            "PINCHATMESSAGE" => await call.ChangeAsync(Change.Pin),
            "UNPINCHATMESSAGE" or "UNPINALLCHATMESSAGES" => await call.ChangeAsync(Change.Unpin),
            "ANSWERCALLBACKQUERY" => await call.AnswerAsync(),
            "SETMYCOMMANDS" => await call.SetCommandsAsync(clear: false),
            "DELETEMYCOMMANDS" => await call.SetCommandsAsync(clear: true),
            "GETMYCOMMANDS" => await call.GetCommandsAsync(),
            "GETFILE" => await call.GetFileAsync(),
            "SETMYPROFILEPHOTO" => await call.SetProfilePhotoAsync(),
            "REMOVEMYPROFILEPHOTO" => await call.RemoveProfilePhotoAsync(),
            // TG-09: Dalan has no webhook to delete, and shows no "typing" yet. Libraries call both and expect true.
            "DELETEWEBHOOK" or "SENDCHATACTION" => TelegramJson.Ok(true),
            "SETCHATMENUBUTTON" => call.SetMenuButton(),
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
            if (ReplyMarkup.Read(parameters, out var keyboard, out var inline, out var keyboardOptions) is { } problem) return TelegramJson.BadRequest(problem);

            // REL-50: the length is only a label; a negative or broken value is dropped.
            var duration = parameters.Number("duration") is >= 0 and <= int.MaxValue and var seconds ? (int)seconds : (int?)null;
            NewFile? file = null;
            await using var content = kind is { } k ? parameters.File(FileFields[k])?.OpenReadStream() : null;
            if (kind is { } fileKind)
            {
                var field = FileFields[fileKind];
                if (parameters.File(field) is { } part) file = new NewFile(fileKind, content, part.FileName, part.ContentType, Duration: duration);
                else if (parameters.Text(field) is not { Length: > 0 } fileId || fileId.StartsWith("attach://", StringComparison.Ordinal))
                    return TelegramJson.BadRequest($"there is no {field} in the request");
                else if (fileId.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || fileId.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    return TelegramJson.BadRequest("sending a file by URL is not supported by Dalan; upload it or send a file_id");
                else file = new NewFile(fileKind, FileId: fileId, Duration: duration);
            }

            if (Formatted(file is null ? "text" : "caption", out var text, out var entities) is { } formatProblem)
                return TelegramJson.BadRequest(formatProblem);
            if (ReplyParameters(out var replyTo, out var withoutReply) is { } replyProblem) return TelegramJson.BadRequest(replyProblem);

            var outgoing = new OutgoingMessage(text ?? "", keyboard, inline, file, keyboardOptions, entities, replyTo, withoutReply);
            var (error, message) = await gateway.SendAsync(bot.Id, userId, outgoing, cancellationToken);
            return error == BotError.None ? TelegramJson.Ok(MessageJson.Sent(bot, message!)) : ErrorOf(error, "send", file is not null);
        }

        /// <summary>TG-16: editMessageText, editMessageCaption and editMessageReplyMarkup (<paramref name="part"/> null).</summary>
        public async Task<IResult> EditAsync(EditPart? part)
        {
            if (parameters.Has("inline_message_id")) return TelegramJson.BadRequest("inline messages are not supported by Dalan");
            if (parameters.Number("chat_id") is not { } userId) return TelegramJson.BadRequest("chat not found");
            if (parameters.Number("message_id") is not { } messageId) return TelegramJson.BadRequest("message to edit not found");
            if (ReplyMarkup.ReadInline(parameters, out var inline) is { } problem) return TelegramJson.BadRequest(problem);

            string? text = null;
            List<TextEntity>? entities = null;
            if (part is not null && Formatted(part == EditPart.Caption ? "caption" : "text", out text, out entities) is { } formatProblem)
                return TelegramJson.BadRequest(formatProblem);

            // A missing caption removes it, as in Telegram; editMessageText always has a text.
            var edit = new MessageEdit(part is null ? null : text ?? "", entities, inline, part ?? EditPart.Any);
            var (error, message) = await gateway.EditAsync(bot.Id, userId, messageId, edit, cancellationToken);
            return error == BotError.None ? TelegramJson.Ok(MessageJson.Sent(bot, message!)) : ErrorOf(error, "edit", part == EditPart.Caption);
        }

        /// <summary>
        /// TG-25: the text or caption in <paramref name="field"/> with its entities: from parse_mode "HTML", else from
        /// "entities" / "caption_entities". Kinds Dalan does not draw (mention, url, custom_emoji...) are left out.
        /// </summary>
        private string? Formatted(string field, out string? text, out List<TextEntity>? entities)
        {
            text = parameters.Text(field);
            entities = null;
            var mode = parameters.Text("parse_mode");
            if (text is not null && string.Equals(mode, "HTML", StringComparison.OrdinalIgnoreCase))
            {
                if (TelegramHtml.Parse(text, out var plain, out var parsed) is { } problem) return $"can't parse entities: {problem}";

                text = plain;
                entities = parsed.Count > 0 ? parsed : null;
                return null;
            }

            var entitiesField = field == "caption" ? "caption_entities" : "entities";
            if (!parameters.TryArray(entitiesField, out var given)) return $"can't parse {entitiesField}: it must be a JSON list";
            if (given is not { } list) return null;

            try
            {
                entities = [.. list.Deserialize<List<EntityJson>>(TelegramJson.ReadOptions)!
                    .Where(e => e is not null && TelegramEntityKinds.Contains(e.Type))
                    .Select(e => new TextEntity(e.Type, e.Offset, e.Length, e.Url, e.Language))];
            }
            catch (JsonException)
            {
                return $"can't parse {entitiesField}: each one needs type, offset and length";
            }

            if (entities.Count == 0) entities = null;
            return null;
        }

        private static readonly HashSet<string> TelegramEntityKinds = new(StringComparer.Ordinal)
        {
            "bold", "italic", "underline", "strikethrough", "spoiler", "code", "pre", "text_link", "blockquote", "expandable_blockquote",
        };

        /// <summary>TG-24: reply_parameters, or the older reply_to_message_id and allow_sending_without_reply.</summary>
        private string? ReplyParameters(out long? replyTo, out bool withoutReply)
        {
            replyTo = parameters.Number("reply_to_message_id");
            withoutReply = parameters.Flag("allow_sending_without_reply");
            if (!parameters.TryObject("reply_parameters", out var given)) return "can't parse reply parameters: they must be a JSON object";
            if (given is not { } reply) return null;

            if (reply.TryGetProperty("chat_id", out _)) return "replies to messages of other chats are not supported by Dalan";
            if (!reply.TryGetProperty("message_id", out var id) || !id.TryGetInt64(out var messageId))
                return "can't parse reply parameters: message_id is missing";

            replyTo = messageId;
            if (reply.TryGetProperty("allow_sending_without_reply", out var allow)) withoutReply = allow.ValueKind == JsonValueKind.True;
            return null;
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

        /// <summary>TG-17: Telegram allows 1 to 100 ids and skips the ones it cannot find.</summary>
        public async Task<IResult> DeleteManyAsync()
        {
            if (parameters.Number("chat_id") is not { } userId) return TelegramJson.BadRequest("chat not found");

            List<long> ids;
            try
            {
                ids = parameters.TryArray("message_ids", out var given) && given is { } list
                    ? list.Deserialize<List<long>>(TelegramJson.ReadOptions)!
                    : [];
            }
            catch (JsonException)
            {
                ids = [];
            }

            if (ids.Count is < 1 or > 100) return TelegramJson.BadRequest("message_ids must be a JSON list of 1 to 100 message ids");

            foreach (var id in ids)
            {
                var error = await gateway.DeleteAsync(bot.Id, userId, id, cancellationToken);
                if (error is not BotError.None and not BotError.MessageNotFound) return ErrorOf(error, "delete", withFile: false);
            }

            return TelegramJson.Ok(true);
        }

        /// <summary>TG-09: Dalan's chat already offers the bot's commands; it has no Mini Apps yet.</summary>
        public IResult SetMenuButton()
        {
            if (!parameters.TryObject("menu_button", out var button)) return TelegramJson.BadRequest("can't parse menu_button: it must be a JSON object");

            var type = button is { } b && b.TryGetProperty("type", out var t) ? t.GetString() : "default";
            return type is "commands" or "default"
                ? TelegramJson.Ok(true)
                : TelegramJson.BadRequest($"{type} menu button is not supported by Dalan");
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

        /// <summary>TG-23: the photo is an InputProfilePhoto whose "photo" is attach://part. It becomes the bot icon (REL-51).</summary>
        public async Task<IResult> SetProfilePhotoAsync()
        {
            if (!parameters.TryObject("photo", out var photo) || photo is not { } input)
                return TelegramJson.BadRequest("can't parse photo: it must be a JSON object (InputProfilePhoto)");

            var type = input.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
            if (type == "animated") return TelegramJson.BadRequest("animated profile photos are not supported by Dalan");
            if (type != "static") return TelegramJson.BadRequest("IMAGE_PROCESS_FAILED");

            var reference = input.TryGetProperty("photo", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
            if (parameters.AttachedFile(reference) is not { } part) return TelegramJson.BadRequest("IMAGE_PROCESS_FAILED");

            await using var content = part.OpenReadStream();
            return await gateway.SetIconAsync(bot.Id, content, cancellationToken) switch
            {
                BotError.None => TelegramJson.Ok(true),
                BotError.ImageTooBig => TelegramJson.BadRequest("photo is too big"),
                _ => TelegramJson.BadRequest("IMAGE_PROCESS_FAILED"),
            };
        }

        public async Task<IResult> RemoveProfilePhotoAsync()
        {
            await gateway.RemoveIconAsync(bot.Id, cancellationToken);
            return TelegramJson.Ok(true);
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
                "the keyboard has too many buttons, a button text is empty or too long, a button style is not success, danger or primary, " +
                "or an inline button has not exactly one of callback_data, an http(s) url and a switch_inline_query"),
            BotError.MessageNotFound => TelegramJson.BadRequest($"message to {action} not found"),
            BotError.NotEditable => TelegramJson.BadRequest("message can't be edited"),
            BotError.NoText => TelegramJson.BadRequest("there is no text in the message to edit"),
            BotError.NoCaption => TelegramJson.BadRequest("there is no caption in the message to edit"),
            BotError.ReplyNotFound => TelegramJson.BadRequest("message to be replied not found"),
            BotError.BadEntities => TelegramJson.BadRequest(
                "can't parse entities: an entity is outside the text, two entities cross, there are too many, or a text_link has no http(s) url"),
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
