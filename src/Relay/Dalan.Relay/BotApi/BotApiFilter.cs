using Dalan.Relay.Contracts;
using Microsoft.AspNetCore.Http;

namespace Dalan.Relay.BotApi;

/// <summary>Runs before every core Bot API call: token check (REL-04), the access log (ADM-01), then the limit per bot (SEC-01).</summary>
internal sealed class BotApiFilter(IBotGateway gateway) : IEndpointFilter
{
    private const string Scheme = "Bearer ";
    private static readonly object BotIdKey = new();

    public static long BotId(HttpContext http) => (long)http.Items[BotIdKey]!;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;

        var header = http.Request.Headers.Authorization.ToString();
        var token = header.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase) ? header[Scheme.Length..].Trim() : null;
        var access = await gateway.EnterAsync(
            token, http.Connection.RemoteIpAddress, http.Connection.RemotePort, http.RequestAborted);
        if (access.RetryAfter is { } wait) return ApiJson.TooManyRequests(http, wait);
        if (access.Suspended) return ApiJson.BotSuspended();
        if (access.Bot is null)
        {
            return ApiJson.Error(StatusCodes.Status401Unauthorized, "unauthorized",
                "The bot token is missing or wrong. Send it as 'Authorization: Bearer <token>'.");
        }

        http.Items[BotIdKey] = access.Bot.Id;
        return await next(context);
    }
}
