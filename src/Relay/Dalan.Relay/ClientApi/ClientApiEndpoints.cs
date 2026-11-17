using Dalan.Relay.Chats;
using Dalan.Relay.Data;
using Dalan.Relay.Live;
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
        api.MapGet("/events", EventStream.RunAsync);
    }

    private static IResult Settings(IOptionsMonitor<RelayOptions> options)
    {
        var relay = options.CurrentValue;
        return ApiJson.Ok(new ClientSettingsJson(
            relay.MaxTextLength, relay.MaxMessagesPerSync, relay.ClientReconnectMinSeconds, relay.ClientReconnectMaxSeconds));
    }

    /// <summary>REL-14: the bots this user has a chat with, newest chat first.</summary>
    private static async Task<IResult> ListBotsAsync(HttpContext http, IDbContextFactory<RelayDb> dbFactory)
    {
        var userId = ClientApiFilter.UserId(http);
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var bots = await db.Memberships
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.StartedAt)
            .Join(db.Bots, m => m.BotId, b => b.Id, (_, bot) => bot)
            .AsNoTracking()
            .ToListAsync(http.RequestAborted);

        return ApiJson.Ok(new BotsResponse(bots.Select(BotJson.Of)));
    }

    /// <summary>REL-13: this is how a person reaches a bot from its link (/@name).</summary>
    private static async Task<IResult> FindBotAsync(string username, HttpContext http, IDbContextFactory<RelayDb> dbFactory)
    {
        var userId = ClientApiFilter.UserId(http);
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
#pragma warning disable CA1304, CA1311, CA1862 // This runs in PostgreSQL as lower(username), which the unique index covers.
        var bot = await db.Bots.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Username.ToLower() == username.ToLower(), http.RequestAborted);
#pragma warning restore CA1304, CA1311, CA1862
        if (bot is null) return BotNotFound();

        var started = await db.Memberships.AnyAsync(m => m.BotId == bot.Id && m.UserId == userId, http.RequestAborted);
        return ApiJson.Ok(new FoundBotJson(bot.Id, bot.Username, bot.Name, started));
    }

    /// <summary>REL-15: a person writes to a bot. The first message starts the chat.</summary>
    private static async Task<IResult> SendMessageAsync(
        long botId, HttpContext http, ChatService chats, IOptionsMonitor<RelayOptions> options)
    {
        if (await ApiJson.ReadAsync<UserMessageRequest>(http) is not { Text: { } text })
            return ApiJson.BadRequest("""Send JSON like {"text": "hello"} with Content-Type application/json.""");

        var (error, message) = await chats.ReceiveFromUserAsync(botId, ClientApiFilter.UserId(http), text, http.RequestAborted);
        return error switch
        {
            ChatError.None => ApiJson.Ok(ChatMessageJson.Of(message!)),
            ChatError.TextEmpty => ApiJson.Error(StatusCodes.Status400BadRequest, "text_empty",
                "'text' is empty. Send at least one character."),
            ChatError.TextTooLong => ApiJson.Error(StatusCodes.Status400BadRequest, "text_too_long",
                $"'text' is longer than {options.CurrentValue.MaxTextLength} characters. Send it as several messages."),
            ChatError.UnknownBot => BotNotFound(),
            _ => ApiJson.Error(StatusCodes.Status401Unauthorized, "unauthorized",
                "This account does not exist any more. Log in again."),
        };
    }

    /// <summary>REL-16: one page of a chat. "after" is the sync after a reconnect; "before" loads older messages.</summary>
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
            // Sync: the oldest messages after the one the client has, so it can ask again for the rest.
            page = await chat.Where(m => m.Seq > after).OrderBy(m => m.Seq).Take(take).ToListAsync(http.RequestAborted);
        }
        else
        {
            page = await chat.OrderByDescending(m => m.Seq).Take(take).ToListAsync(http.RequestAborted);
            page.Reverse();
        }

        return ApiJson.Ok(new MessagesResponse(page.Select(ChatMessageJson.Of)));
    }

    private static IResult BotNotFound() => ApiJson.Error(StatusCodes.Status404NotFound, "bot_not_found",
        "There is no bot with this username or id. Check the link.");
}
