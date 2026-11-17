using System.Globalization;
using System.Security.Claims;
using Dalan.Admin.Contracts;
using Dalan.Identity.Contracts;
using Microsoft.AspNetCore.Http;

namespace Dalan.Admin.AdminApi;

/// <summary>ADM-05: runs before every admin call, after ASP.NET Core checked the session: the access log, then "is this an admin?".</summary>
internal sealed class AdminFilter(IUserDirectory users, IAccessLog accessLog) : IEndpointFilter
{
    /// <summary>"sub" is the standard JWT claim for the subject; Identity puts the user id there.</summary>
    public static long UserId(HttpContext http) =>
        long.Parse(http.User.FindFirstValue("sub")!, CultureInfo.InvariantCulture);

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var userId = UserId(http);
        await accessLog.RecordAsync(
            AccessActor.User, userId, http.Connection.RemoteIpAddress, http.Connection.RemotePort, http.RequestAborted);

        return (await users.GetAccessAsync(userId, http.RequestAborted)).IsAdmin
            ? await next(context)
            : AdminJson.Error(StatusCodes.Status403Forbidden, "not_admin", "Only an admin of Dalan may do this.");
    }
}
