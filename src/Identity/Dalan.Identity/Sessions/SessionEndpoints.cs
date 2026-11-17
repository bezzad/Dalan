using Dalan.Identity.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Dalan.Identity.Sessions;

internal static class SessionEndpoints
{
    public static void MapSessions(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/me", MeAsync).RequireAuthorization();
        endpoints.MapPost("/api/logout", Logout);
    }

    /// <summary>AUTH-04: the public profile only. Never the phone number.</summary>
    private static async Task<IResult> MeAsync(HttpContext http, IUserDirectory users)
    {
        var user = await users.FindAsync(SessionTokens.UserId(http.User), http.RequestAborted);
        return user is null ? SessionTokens.Unauthorized() : Results.Json(ProfileJson.Of(user));
    }

    private static IResult Logout(HttpContext http)
    {
        SessionTokens.SignOut(http);
        return Results.NoContent();
    }
}

internal sealed record ProfileJson(long Id, string Name, string? Username)
{
    public static ProfileJson Of(UserProfile user) => new(user.Id, user.DisplayName, user.Username);
}
