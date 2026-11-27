using System.Globalization;
using Dalan.Admin.Contracts;
using Dalan.Identity.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Identity.Sessions;

/// <summary>AUTH-35: one session in the list. <see cref="Address"/> is null when the address is unknown.</summary>
internal sealed record SessionJson(
    long Id, string Browser, string System, string? Address, DateTimeOffset CreatedAt, DateTimeOffset LastUsedAt, bool Current);

internal sealed record SessionsResponse(IEnumerable<SessionJson> Sessions);

/// <summary>AUTH-35: the user's live sessions, and ending one or all others. Only with a session. Each call writes the access log.</summary>
internal static class SessionListEndpoints
{
    public static void MapSessionList(this IEndpointRouteBuilder endpoints)
    {
        var sessions = endpoints.MapGroup("/api/account/sessions").RequireAuthorization().AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            await http.RequestServices.GetRequiredService<IAccessLog>().RecordAsync(AccessActor.User,
                SessionTokens.UserId(http.User), http.Connection.RemoteIpAddress, http.Connection.RemotePort, http.RequestAborted);
            return await next(context);
        });
        sessions.MapGet("", ListAsync);
        sessions.MapDelete("", EndOthersAsync);
        sessions.MapDelete("/{sessionId:long}", EndAsync);
    }

    private static async Task<IResult> ListAsync(
        HttpContext http, IDbContextFactory<IdentityDb> dbFactory, SessionTokens tokens, TimeProvider time)
    {
        var userId = SessionTokens.UserId(http.User);
        var current = await tokens.SessionIdAsync(http, userId);
        var now = time.GetUtcNow();
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var rows = await db.Sessions.AsNoTracking().Where(s => s.UserId == userId && s.ExpiresAt > now)
            .OrderByDescending(s => s.RotatedAt).ThenByDescending(s => s.Id)
            .ToListAsync(http.RequestAborted);
        return AuthJson.Ok(new SessionsResponse(rows.Select(s =>
            new SessionJson(s.Id, s.Browser, s.Os, s.Address?.ToString(), s.CreatedAt, s.RotatedAt, s.Id == current))));
    }

    private static async Task<IResult> EndAsync(
        long sessionId, HttpContext http, IDbContextFactory<IdentityDb> dbFactory, IAuditLog audit, SecurityLog log)
    {
        var userId = SessionTokens.UserId(http.User);
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var ended = await db.Sessions.Where(s => s.Id == sessionId && s.UserId == userId).ExecuteDeleteAsync(http.RequestAborted);
        if (ended == 0) return AuthJson.Error(StatusCodes.Status404NotFound, "session_not_found", "You have no session with this id.");

        log.SessionsEnded(userId, ended);
        await audit.RecordAsync(userId, "session.end", string.Create(CultureInfo.InvariantCulture, $"session {sessionId}"), null,
            http.RequestAborted);
        return Results.NoContent();
    }

    private static async Task<IResult> EndOthersAsync(HttpContext http, SessionTokens tokens, IAuditLog audit, SecurityLog log)
    {
        var userId = SessionTokens.UserId(http.User);
        var ended = await tokens.SignOutOtherDevicesAsync(http, userId);
        log.SessionsEnded(userId, ended);
        await audit.RecordAsync(userId, "session.end_others", string.Create(CultureInfo.InvariantCulture, $"user {userId}"),
            ended.ToString(CultureInfo.InvariantCulture), http.RequestAborted);
        return Results.NoContent();
    }
}
