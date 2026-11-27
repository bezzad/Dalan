using Dalan.Identity.Users;
using Dalan.Notification.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
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
    private static async Task<IResult> MeAsync(HttpContext http, UserAdmin users)
    {
        var profile = await users.ProfileAsync(SessionTokens.UserId(http.User), http.RequestAborted);
        return profile is null ? AuthJson.Unauthorized() : AuthJson.Ok(profile);
    }


    private static async Task<IResult> LogoutAsync(HttpContext http, SessionTokens sessions)
    {
        await sessions.SignOutAsync(http);
        return Results.NoContent();
    }

    private static async Task<IResult> RefreshAsync(HttpContext http, SessionTokens sessions) =>
        await sessions.RefreshAsync(http) ? Results.NoContent() : AuthJson.Unauthorized();

    /// <summary>AUTH-16: what the login screens must know before anyone is logged in.</summary>
    private static IResult Settings(IOptionsMonitor<IdentityOptions> options, IHostEnvironment environment, IEmailSender email) =>
        AuthJson.Ok(new
        {
            DevLogin = options.CurrentValue.DevLogin && environment.IsDevelopment(),
            EmailLogin = email.IsOn,
            GithubLogin = options.CurrentValue.GitHub.IsOn,
            options.CurrentValue.PasswordMinLength,
            NameMaxLength = options.CurrentValue.MaxNameLength,
            // AUTH-36: the client shows a word of the user's language in the delete card.
            DeleteWords = DeleteWords(options.CurrentValue),
        });

    /// <summary>AUTH-36: the words that confirm "delete my account" (Identity:DeleteConfirmWords).</summary>
    public static string[] DeleteWords(IdentityOptions options) =>
        (options.DeleteConfirmWords ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
