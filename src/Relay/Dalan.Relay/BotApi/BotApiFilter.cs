using System.Globalization;
using Dalan.Relay.Bots;
using Microsoft.AspNetCore.Http;

namespace Dalan.Relay.BotApi;

/// <summary>Runs before every Bot API call: limit per address, token check (REL-04), limit per bot.</summary>
internal sealed class BotApiFilter(BotRegistry bots, BotApiLimits limits) : IEndpointFilter
{
    private const string Scheme = "Bearer ";
    private static readonly object BotIdKey = new();

    public static long BotId(HttpContext http) => (long)http.Items[BotIdKey]!;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;

        // Before the token check, so a flood of wrong tokens never reaches the database.
        var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (limits.CheckIp(ip) is { } ipWait) return TooManyRequests(http, ipWait);

        var header = http.Request.Headers.Authorization.ToString();
        var token = header.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase) ? header[Scheme.Length..].Trim() : null;
        if (await bots.AuthenticateAsync(token, http.RequestAborted) is not { } botId)
        {
            return BotApiJson.Error(StatusCodes.Status401Unauthorized, "unauthorized",
                "The bot token is missing or wrong. Send it as 'Authorization: Bearer <token>'.");
        }

        // After the token check, so a wrong token cannot use up a real bot's limit.
        if (limits.CheckBot(botId) is { } botWait) return TooManyRequests(http, botWait);

        http.Items[BotIdKey] = botId;
        return await next(context);
    }

    private static IResult TooManyRequests(HttpContext http, TimeSpan wait)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds));
        http.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        return BotApiJson.Error(StatusCodes.Status429TooManyRequests, "rate_limited",
            $"Too many calls. Wait {seconds} second(s) and try again.");
    }
}
