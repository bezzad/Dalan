using Dalan.Relay.Chats;
using Dalan.Relay.Data;
using Dalan.Relay.Queue;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dalan.Relay.BotApi;

/// <summary>The door for bots: Dalan's own Bot API. The Telegram adapter is a separate plugin.</summary>
internal static class BotApiEndpoints
{
    public static void MapBotApi(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/v1").AddEndpointFilter<BotApiFilter>();
        api.MapGet("/updates", GetUpdatesAsync);
        api.MapPost("/messages", SendMessageAsync);
    }

    private static async Task<IResult> GetUpdatesAsync(
        HttpContext http,
        IUpdateQueue queue,
        IDbContextFactory<RelayDb> dbFactory,
        IOptionsMonitor<RelayOptions> options,
        TimeProvider time,
        IHostApplicationLifetime lifetime)
    {
        if (PollRequest.Parse(http.Request.Query, options.CurrentValue, out var error) is not { } poll)
            return ApiJson.BadRequest(error);

        var botId = BotApiFilter.BotId(http);
        var replaced = queue.BeginPoll(botId);
        using var timeout = new CancellationTokenSource(poll.Timeout, time);
        using var endOfWait = CancellationTokenSource.CreateLinkedTokenSource(
            replaced, timeout.Token, lifetime.ApplicationStopping, http.RequestAborted);

        while (!replaced.IsCancellationRequested)
        {
            var batch = queue.Take(botId, poll.Offset, poll.Limit);
            await RelayDb.MarkAsync(dbFactory, batch.Delivered, DeliveryState.Delivered);
            await RelayDb.MarkAsync(dbFactory, batch.NotDelivered, DeliveryState.NotDelivered);
            if (batch.Updates.Count > 0) return Updates(batch.Updates);

            try
            {
                await batch.Arrival.WaitAsync(endOfWait.Token);
            }
            catch (OperationCanceledException) when (!replaced.IsCancellationRequested)
            {
                // Timeout (Q-06), server stopping (Q-09) or the bot went away: nothing to give.
                return Updates([]);
            }
            catch (OperationCanceledException)
            {
            }
        }

        return ApiJson.Error(StatusCodes.Status409Conflict, "conflict",
            "A newer get-updates call with the same token replaced this one. Run only one copy of the bot.");
    }

    private static IResult Updates(IReadOnlyList<QueuedUpdate> updates) =>
        ApiJson.Ok(new UpdatesResponse(updates.Select(UpdateJson.Of)));

    private static async Task<IResult> SendMessageAsync(HttpContext http, ChatService chats, IOptionsMonitor<RelayOptions> options)
    {
        var request = await ApiJson.ReadAsync<SendMessageRequest>(http);
        if (request is not { UserId: { } userId, Text: { } text })
            return ApiJson.BadRequest("""Send JSON like {"user_id": 1, "text": "hello"} with Content-Type application/json.""");

        var (error, message) = await chats.SendFromBotAsync(
            BotApiFilter.BotId(http), userId, text, request.ReplyKeyboard, http.RequestAborted);
        return error switch
        {
            ChatError.None => ApiJson.Ok(SentMessageJson.Of(message!)),
            ChatError.TextEmpty => ApiJson.Error(StatusCodes.Status400BadRequest, "text_empty",
                "'text' is empty. Send at least one character."),
            ChatError.TextTooLong => ApiJson.Error(StatusCodes.Status400BadRequest, "text_too_long",
                $"'text' is longer than {options.CurrentValue.MaxTextLength} characters. Send it as several messages."),
            ChatError.BadKeyboard => ApiJson.Error(StatusCodes.Status400BadRequest, "bad_keyboard",
                $"'reply_keyboard' must be rows of button texts: at most {options.CurrentValue.MaxKeyboardButtons} buttons, " +
                $"each text 1 to {options.CurrentValue.MaxButtonTextLength} characters."),
            _ => ApiJson.Error(StatusCodes.Status403Forbidden, "chat_not_active",
                "This user has not started the bot, or has stopped it. A bot can write only to users who started it."),
        };
    }
}
