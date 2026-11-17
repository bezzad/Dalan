using Dalan.Relay.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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

    private static async Task<IResult> SendMessageAsync(HttpContext http, IBotGateway gateway, IOptionsMonitor<RelayOptions> options)
    {
        var request = await ApiJson.ReadAsync<SendMessageRequest>(http);
        if (request is not { UserId: { } userId, Text: { } text })
            return ApiJson.BadRequest("""Send JSON like {"user_id": 1, "text": "hello"} with Content-Type application/json.""");

        var (error, message) = await gateway.SendMessageAsync(
            BotApiFilter.BotId(http), userId, text, request.ReplyKeyboard, http.RequestAborted);
        return error switch
        {
            SendError.None => ApiJson.Ok(SentMessageJson.Of(message!)),
            SendError.TextEmpty => ApiJson.Error(StatusCodes.Status400BadRequest, "text_empty",
                "'text' is empty. Send at least one character."),
            SendError.TextTooLong => ApiJson.Error(StatusCodes.Status400BadRequest, "text_too_long",
                $"'text' is longer than {options.CurrentValue.MaxTextLength} characters. Send it as several messages."),
            SendError.BadKeyboard => ApiJson.Error(StatusCodes.Status400BadRequest, "bad_keyboard",
                $"'reply_keyboard' must be rows of button texts: at most {options.CurrentValue.MaxKeyboardButtons} buttons, " +
                $"each text 1 to {options.CurrentValue.MaxButtonTextLength} characters."),
            _ => ApiJson.Error(StatusCodes.Status403Forbidden, "chat_not_active",
                "This user has not started the bot, or has stopped it. A bot can write only to users who started it."),
        };
    }
}
