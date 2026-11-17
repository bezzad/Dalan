using Dalan.Admin.Contracts;
using Dalan.Relay.Bots;
using Microsoft.AspNetCore.Http;

namespace Dalan.Relay.BotApi;

/// <summary>Runs before every Bot API call: token check (REL-04), the access log (ADM-01), then the limit per bot (SEC-01).</summary>
internal sealed class BotApiFilter(BotRegistry bots, RateLimits limits, IAccessLog accessLog) : IEndpointFilter
{
    private const string Scheme = "Bearer ";
    private static readonly object BotIdKey = new();

    public static long BotId(HttpContext http) => (long)http.Items[BotIdKey]!;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;

        var header = http.Request.Headers.Authorization.ToString();
        var token = header.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase) ? header[Scheme.Length..].Trim() : null;
        if (await bots.AuthenticateAsync(token, http.RequestAborted) is not { } botId)
        {
            return ApiJson.Error(StatusCodes.Status401Unauthorized, "unauthorized",
                "The bot token is missing or wrong. Send it as 'Authorization: Bearer <token>'.");
        }

        await accessLog.RecordAsync(
            AccessActor.Bot, botId, http.Connection.RemoteIpAddress, http.Connection.RemotePort, http.RequestAborted);

        // After the token check, so a wrong token cannot use up a real bot's limit.
        if (limits.CheckBot(botId) is { } wait) return ApiJson.TooManyRequests(http, wait);

        http.Items[BotIdKey] = botId;
        return await next(context);
    }
}
