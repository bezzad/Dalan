using Dalan.Relay.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(string token, string method, HttpContext http, IBotGateway gateway)
    {
        var access = await gateway.EnterAsync(token, http.RequestAborted);
        if (access.RetryAfter is { } wait) return TelegramJson.TooManyRequests(http, wait);
        if (access.Bot is not { } bot) return TelegramJson.Error(StatusCodes.Status401Unauthorized, "Unauthorized");

        return method.ToUpperInvariant() switch
        {
            "GETME" => TelegramJson.Ok(UserJson.Of(bot)),
            "GETUPDATES" => await GetUpdatesAsync(bot, http, gateway),
            "SENDMESSAGE" => await SendMessageAsync(bot, http, gateway),
            // TG-09: Dalan has no webhook to delete, and shows no "typing" yet. Libraries call both and expect true.
            "DELETEWEBHOOK" or "SENDCHATACTION" => TelegramJson.Ok(true),
            _ => TelegramJson.BadRequest($"method {method} is not supported by Dalan"),
        };
    }

    private static async Task<IResult> GetUpdatesAsync(BotInfo bot, HttpContext http, IBotGateway gateway)
    {
        if (await TelegramParameters.ReadAsync(http) is not { } parameters) return TelegramJson.BadBody();

        // TG-05: the gateway cuts every value to its range, so a negative offset counts as 0.
        var updates = await gateway.GetUpdatesAsync(
            bot.Id, parameters.Number("offset") ?? 0, parameters.Number("limit"), parameters.Number("timeout"), http.RequestAborted);

        return updates is null
            ? TelegramJson.Error(StatusCodes.Status409Conflict,
                "Conflict: terminated by other getUpdates request; make sure that only one bot instance is running")
            : TelegramJson.Ok(updates.Select(UpdateJson.Of));
    }

    private static async Task<IResult> SendMessageAsync(BotInfo bot, HttpContext http, IBotGateway gateway)
    {
        if (await TelegramParameters.ReadAsync(http) is not { } parameters) return TelegramJson.BadBody();

        // A chat is one user, so chat_id is the user's id. A channel name can never be one.
        if (parameters.Number("chat_id") is not { } userId) return TelegramJson.BadRequest("chat not found");
        if (ReplyMarkup.Read(parameters, out var keyboard) is { } problem) return TelegramJson.BadRequest(problem);

        var (error, message) = await gateway.SendMessageAsync(
            bot.Id, userId, parameters.Text("text") ?? "", keyboard, http.RequestAborted);
        return error switch
        {
            SendError.None => TelegramJson.Ok(MessageJson.Sent(bot, message!)),
            SendError.TextEmpty => TelegramJson.BadRequest("message text is empty"),
            SendError.TextTooLong => TelegramJson.BadRequest("message is too long"),
            SendError.BadKeyboard => TelegramJson.BadRequest(
                "the reply keyboard has too many buttons, or a button text is empty or too long"),
            _ => TelegramJson.Error(StatusCodes.Status403Forbidden, "Forbidden: bot can't initiate conversation with a user"),
        };
    }
}
