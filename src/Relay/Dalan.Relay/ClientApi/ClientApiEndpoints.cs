using Dalan.Relay.BotApi;
using Dalan.Relay.Bots;
using Dalan.Relay.Buttons;
using Dalan.Relay.Builder;
using Dalan.Relay.Chats;
using Dalan.Relay.Data;
using Dalan.Relay.Files;
using Dalan.Relay.Live;
using Dalan.Relay.Mcp;
using Dalan.Relay.Push;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dalan.Relay.ClientApi;

/// <summary>The door for people: what the PWA calls. It never runs bot logic; the bot at home does.</summary>
internal static class ClientApiEndpoints
{
    public static void MapClientApi(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api").RequireAuthorization().AddEndpointFilter<ClientApiFilter>();
        api.MapGet("/settings", Settings);
        api.MapGet("/bots", ListBotsAsync);
        api.MapGet("/bots/by-username/{username}", FindBotAsync);
        api.MapPost("/bots/{botId:long}/messages", SendMessageAsync);
        api.MapGet("/bots/{botId:long}/messages", GetMessagesAsync);
        api.MapPost("/bots/{botId:long}/files", SendFileAsync);
        api.MapGet("/bots/{botId:long}/files/{fileId}", GetFileAsync);
        api.MapPost("/bots/{botId:long}/callbacks", PressAsync);
        api.MapGet("/bots/{botId:long}/commands", GetCommandsAsync);
        api.MapPost("/bots/{botId:long}/read", ReadAsync);
        api.MapGet("/bots/{botId:long}/status", StatusAsync);
        api.MapGet("/events", EventStream.RunAsync);
        api.MapPush();
        api.MapBuilder();
        api.MapMcpDoor();
    }

    private static IResult Settings(IOptionsMonitor<RelayOptions> options)
    {
        var relay = options.CurrentValue;
        return ApiJson.Ok(new ClientSettingsJson(
            relay.MaxTextLength, relay.MaxCaptionLength, relay.MaxFileSizeMb, relay.MaxMessagesPerSync,
            relay.ClientReconnectMinSeconds, relay.ClientReconnectMaxSeconds, relay.ClientStatusRefreshSeconds));
    }

    /// <summary>REL-14: the bots this user has a chat with, newest chat first.</summary>
    private static async Task<IResult> ListBotsAsync(HttpContext http, IDbContextFactory<RelayDb> dbFactory, BotPresence presence)
    {
        var userId = ClientApiFilter.UserId(http);
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var bots = await db.Memberships
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.StartedAt)
            // REL-49: a deleted bot leaves the user's list; its chat stays for the admin.
            .Join(db.Bots.Where(b => b.DeletedAt == null), m => m.BotId, b => b.Id, (chat, bot) => new
            {
                bot.Id,
                bot.Username,
                bot.Name,
                bot.LastSeenAt,
                Suspended = bot.SuspendedAt != null,
                // REL-32: the same count as in ChatService.ReadAsync.
                Unread = db.Messages.Count(m => m.BotId == chat.BotId && m.UserId == chat.UserId
                    && m.Direction == Direction.FromBot && !m.Deleted && m.Seq > chat.LastReadSeq),
            })
            .ToListAsync(http.RequestAborted);

        return ApiJson.Ok(new BotsResponse(bots.Select(bot =>
        {
            var status = presence.Of(bot.Id, bot.LastSeenAt);
            return new BotJson(bot.Id, bot.Username, bot.Name, bot.Unread, status.Online, status.LastSeen, bot.Suspended);
        })));
    }

    /// <summary>REL-13: this is how a person reaches a bot from its link (/@name).</summary>
    private static async Task<IResult> FindBotAsync(string username, HttpContext http, IDbContextFactory<RelayDb> dbFactory)
    {
        var userId = ClientApiFilter.UserId(http);
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
#pragma warning disable CA1304, CA1311, CA1862 // This runs in PostgreSQL as lower(username), which the unique index covers.
        var bot = await db.Bots.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Username.ToLower() == username.ToLower() && b.DeletedAt == null, http.RequestAborted);
#pragma warning restore CA1304, CA1311, CA1862
        if (bot is null) return BotNotFound();

        var started = await db.Memberships.AnyAsync(m => m.BotId == bot.Id && m.UserId == userId, http.RequestAborted);
        return ApiJson.Ok(new FoundBotJson(bot.Id, bot.Username, bot.Name, started, bot.SuspendedAt is not null));
    }

    /// <summary>REL-15: a person writes to a bot. The first message starts the chat.</summary>
    private static async Task<IResult> SendMessageAsync(
        long botId, HttpContext http, ChatService chats, IOptionsMonitor<RelayOptions> options)
    {
        if (await ApiJson.ReadAsync<UserMessageRequest>(http) is not { Text: { } text })
            return ApiJson.BadRequest("""Send JSON like {"text": "hello"} with Content-Type application/json.""");

        return Sent(await chats.ReceiveFromUserAsync(botId, ClientApiFilter.UserId(http), text, null, http.RequestAborted), options);
    }

    /// <summary>
    /// REL-28: the raw file is the body. The name and caption come in headers, percent-encoded.
    /// SEC-05: the custom header X-File-Name is something a form of another site cannot send.
    /// </summary>
    private static async Task<IResult> SendFileAsync(
        long botId, HttpContext http, ChatService chats, FileStore files, IOptionsMonitor<RelayOptions> options)
    {
        var headers = http.Request.Headers;
        var name = Decode(headers["X-File-Name"]);
        var caption = Decode(headers["X-Caption"]) ?? "";
        if (string.IsNullOrWhiteSpace(name) || http.Request.HasFormContentType)
        {
            return ApiJson.BadRequest(
                "Send the file itself as the body, its type as Content-Type, and its name in the header X-File-Name (percent-encoded).");
        }

        BotApiEndpoints.AllowFileUpload(http, files);
        var upload = new UserUpload(http.Request.Body, name, http.Request.ContentType);
        return Sent(await chats.ReceiveFromUserAsync(botId, ClientApiFilter.UserId(http), caption, upload, http.RequestAborted), options);
    }

    /// <summary>REL-29: only a file that a message of this user's chat carries.</summary>
    private static async Task<IResult> GetFileAsync(
        long botId, string fileId, HttpContext http, IDbContextFactory<RelayDb> dbFactory, ChatService chats, FileStore files)
    {
        var userId = ClientApiFilter.UserId(http);
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var inChat = await db.Messages.AnyAsync(
            m => m.BotId == botId && m.UserId == userId && !m.Deleted && m.Content.File != null && m.Content.File.Id == fileId,
            http.RequestAborted);

        if (inChat && await chats.FindFileAsync(botId, fileId, http.RequestAborted) is { } file && files.Open(file.Id) is { } content)
            return FileResponse.Of(http, file, content);

        return ApiJson.Error(StatusCodes.Status404NotFound, "file_not_found",
            "This file is not in your chat, or it expired. A file is kept for a limited time.");
    }

    /// <summary>REL-21: a press on an inline button.</summary>
    private static async Task<IResult> PressAsync(long botId, HttpContext http, ButtonPresses presses, SuspendedBots suspendedBots)
    {
        if (suspendedBots.Contains(botId)) return ApiJson.BotSuspended();
        if (await ApiJson.ReadAsync<PressRequest>(http) is not { MessageId: { } messageId, Data: { } data })
            return ApiJson.BadRequest("""Send JSON like {"message_id": 2, "data": "yes"} with Content-Type application/json.""");

        return await presses.PressAsync(botId, ClientApiFilter.UserId(http), messageId, data, http.RequestAborted) is { } id
            ? ApiJson.Ok(new PressJson(id))
            : ApiJson.Error(StatusCodes.Status404NotFound, "button_not_found",
                "This message has no such button any more. Load the chat again.");
    }

    /// <summary>REL-32: the user read the chat up to a message.</summary>
    private static async Task<IResult> ReadAsync(long botId, HttpContext http, ChatService chats)
    {
        if (await ApiJson.ReadAsync<ReadRequest>(http) is not { MessageId: { } messageId })
            return ApiJson.BadRequest("""Send JSON like {"message_id": 7} with Content-Type application/json.""");

        return await chats.ReadAsync(botId, ClientApiFilter.UserId(http), messageId, http.RequestAborted) is { } unread
            ? ApiJson.Ok(new UnreadJson(unread))
            : BotNotFound();
    }

    /// <summary>REL-33: is the bot's home computer on?</summary>
    private static async Task<IResult> StatusAsync(long botId, HttpContext http, IDbContextFactory<RelayDb> dbFactory, BotPresence presence)
    {
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var bot = await db.Bots.Where(b => b.Id == botId).Select(b => new { b.LastSeenAt }).FirstOrDefaultAsync(http.RequestAborted);
        if (bot is null) return BotNotFound();

        var status = presence.Of(botId, bot.LastSeenAt);
        return ApiJson.Ok(new StatusJson(status.Online, status.LastSeen));
    }

    /// <summary>REL-26: the bot's command menu.</summary>
    private static async Task<IResult> GetCommandsAsync(long botId, HttpContext http, BotRegistry bots) =>
        ApiJson.Ok(new CommandsJson((await bots.GetCommandsAsync(botId, http.RequestAborted))
            .Select(c => new CommandJson(c.Command, c.Description))));

    private static IResult Sent((ChatError Error, Message? Message) result, IOptionsMonitor<RelayOptions> options) => result.Error switch
    {
        ChatError.None => ApiJson.Ok(ChatMessageJson.Of(result.Message!)),
        ChatError.TextEmpty => ApiJson.Error(StatusCodes.Status400BadRequest, "text_empty",
            "'text' is empty. Send at least one character."),
        ChatError.TextTooLong => ApiJson.Error(StatusCodes.Status400BadRequest, "text_too_long",
            $"The text is too long: at most {options.CurrentValue.MaxTextLength} characters, or " +
            $"{options.CurrentValue.MaxCaptionLength} for a file caption. Send it as several messages."),
        ChatError.FileTooBig => ApiJson.Error(StatusCodes.Status413PayloadTooLarge, "file_too_big",
            $"The file is bigger than {options.CurrentValue.MaxFileSizeMb} MB."),
        ChatError.UploadLimit => ApiJson.Error(StatusCodes.Status429TooManyRequests, "upload_limit",
            $"You sent {options.CurrentValue.UserUploadMbPerDay} MB of files in the last 24 hours. Try again later."),
        ChatError.UnknownBot => BotNotFound(),
        ChatError.BotSuspended => ApiJson.BotSuspended(),
        _ => ApiJson.Error(StatusCodes.Status401Unauthorized, "unauthorized",
            "This account does not exist any more. Log in again."),
    };

    private static string? Decode(string? value)
    {
        try
        {
            return string.IsNullOrEmpty(value) ? null : Uri.UnescapeDataString(value);
        }
        catch (UriFormatException)
        {
            return null;
        }
    }

    /// <summary>REL-16: one page of a chat. "after" (a version) is the sync after a reconnect; "before" (an id) loads older messages.</summary>
    private static async Task<IResult> GetMessagesAsync(
        long botId, HttpContext http, IDbContextFactory<RelayDb> dbFactory, IOptionsMonitor<RelayOptions> options)
    {
        var query = http.Request.Query;
        if (!ApiJson.TryReadNumber(query, "after", out var after, out var error)
            || !ApiJson.TryReadNumber(query, "before", out var before, out error)
            || !ApiJson.TryReadNumber(query, "limit", out var limit, out error))
        {
            return ApiJson.BadRequest(error);
        }

        if (limit < 1) return ApiJson.BadRequest("'limit' must be 1 or more.");

        var max = options.CurrentValue.MaxMessagesPerSync;
        var take = (int)Math.Min(limit ?? max, max);
        var userId = ClientApiFilter.UserId(http);

        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var chat = db.Messages.AsNoTracking().Where(m => m.BotId == botId && m.UserId == userId);
        if (before is not null) chat = chat.Where(m => m.Seq < before);

        List<Message> page;
        if (after is not null)
        {
            // Sync: the oldest changes after the version the client has, so it can ask again for the rest.
            page = await chat.Where(m => m.Version > after).OrderBy(m => m.Version).Take(take).ToListAsync(http.RequestAborted);
        }
        else
        {
            page = await chat.OrderByDescending(m => m.Seq).Take(take).ToListAsync(http.RequestAborted);
            page.Reverse();
        }

        // The pinned message may be older than the page, so it comes on its own.
        var pinned = await db.Messages.AsNoTracking()
            .FirstOrDefaultAsync(m => m.BotId == botId && m.UserId == userId && m.Pinned, http.RequestAborted);
        return ApiJson.Ok(new MessagesResponse(page.Select(ChatMessageJson.Of), pinned is null ? null : ChatMessageJson.Of(pinned)));
    }

    private static IResult BotNotFound() => ApiJson.Error(StatusCodes.Status404NotFound, "bot_not_found",
        "There is no bot with this username or id. Check the link.");
}
