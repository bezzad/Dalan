using Dalan.Identity.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dalan.Identity.Sessions;

internal static class SessionEndpoints
{
    public static void MapSessions(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/me", MeAsync).RequireAuthorization();
        endpoints.MapPost("/api/logout", LogoutAsync);
        endpoints.MapPost("/api/auth/refresh", RefreshAsync);
        endpoints.MapGet("/api/auth/settings", Settings);
    }

    /// <summary>AUTH-04: the profile of the logged-in user.</summary>
    private static async Task<IResult> MeAsync(HttpContext http, IDbContextFactory<IdentityDb> dbFactory)
    {
        var profile = await ProfileAsync(dbFactory, SessionTokens.UserId(http.User), http.RequestAborted);
        return profile is null ? AuthJson.Unauthorized() : AuthJson.Ok(profile);
    }

    public static async Task<ProfileJson?> ProfileAsync(
        IDbContextFactory<IdentityDb> dbFactory, long userId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Users
            .Where(u => u.Id == userId)
            .Select(u => new ProfileJson(u.Id, u.DisplayName, u.Username, u.PasswordHash != null))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static async Task<IResult> LogoutAsync(HttpContext http, SessionTokens sessions)
    {
        await sessions.SignOutAsync(http);
        return Results.NoContent();
    }

    private static async Task<IResult> RefreshAsync(HttpContext http, SessionTokens sessions) =>
        await sessions.RefreshAsync(http) ? Results.NoContent() : AuthJson.Unauthorized();

    /// <summary>AUTH-16: what the login screens must know before anyone is logged in.</summary>
    private static IResult Settings(IOptionsMonitor<IdentityOptions> options, IHostEnvironment environment) => AuthJson.Ok(new
    {
        DevLogin = options.CurrentValue.DevLogin && environment.IsDevelopment(),
        options.CurrentValue.PasswordMinLength,
        NameMaxLength = options.CurrentValue.MaxNameLength,
    });
}
