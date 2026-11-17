using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Dalan.Relay.ClientApi;

/// <summary>
/// Runs before every Client API call, after ASP.NET Core checked the session (REL-12): the limit per user (SEC-03).
/// </summary>
internal sealed class ClientApiFilter(RateLimits limits) : IEndpointFilter
{
    /// <summary>The user of the session. "sub" is the standard JWT claim for the subject; Identity puts the user id there.</summary>
    public static long UserId(HttpContext http) =>
        long.Parse(http.User.FindFirstValue("sub")!, CultureInfo.InvariantCulture);

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        return limits.CheckUser(UserId(http)) is { } wait
            ? ApiJson.TooManyRequests(http, wait)
            : await next(context);
    }
}
