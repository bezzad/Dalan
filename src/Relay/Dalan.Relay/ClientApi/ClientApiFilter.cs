using System.Globalization;
using System.Security.Claims;
using Dalan.Admin.Contracts;
using Microsoft.AspNetCore.Http;

namespace Dalan.Relay.ClientApi;

/// <summary>
/// Runs before every Client API call, after ASP.NET Core checked the session (REL-12):
/// the access log (ADM-01), then the limit per user (SEC-03).
/// </summary>
internal sealed class ClientApiFilter(RateLimits limits, IAccessLog accessLog) : IEndpointFilter
{
    /// <summary>The user of the session. "sub" is the standard JWT claim for the subject; Identity puts the user id there.</summary>
    public static long UserId(HttpContext http) =>
        long.Parse(http.User.FindFirstValue("sub")!, CultureInfo.InvariantCulture);

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var userId = UserId(http);
        await accessLog.RecordAsync(
            AccessActor.User, userId, http.Connection.RemoteIpAddress, http.Connection.RemotePort, http.RequestAborted);

        return limits.CheckUser(userId) is { } wait
            ? ApiJson.TooManyRequests(http, wait)
            : await next(context);
    }
}
