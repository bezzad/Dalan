using System.Threading.RateLimiting;
using Dalan.Identity.Data;
using Dalan.Identity.Sessions;
using Dalan.Identity.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dalan.Identity.Passwords;

/// <summary>SEC-09: at most N password tries per window for each login name. In memory: enough for one process.</summary>
internal sealed class PasswordTries(IOptionsMonitor<IdentityOptions> options) : IAsyncDisposable
{
    private readonly PartitionedRateLimiter<string> _perLogin = PartitionedRateLimiter.Create<string, string>(
        login => RateLimitPartition.GetFixedWindowLimiter(login, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = options.CurrentValue.PasswordTries,
            Window = Window(options.CurrentValue),
            QueueLimit = 0,
        }));

    /// <summary>Returns null when the try may go on, otherwise how long the caller should wait.</summary>
    public TimeSpan? Check(string login)
    {
        using var lease = _perLogin.AttemptAcquire(login);
        if (lease.IsAcquired) return null;

        return lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter) ? retryAfter : Window(options.CurrentValue);
    }

    private static TimeSpan Window(IdentityOptions options) => TimeSpan.FromMinutes(options.PasswordTriesWindowMinutes);

    public ValueTask DisposeAsync() => _perLogin.DisposeAsync();
}

/// <summary>AUTH-14: login with the phone number or the username, and the password.</summary>
internal static class PasswordLogin
{
    private sealed record Request(string? Login, string? Password);

    public static void MapPasswordLogin(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost("/api/auth/password", LoginAsync);

    private static async Task<IResult> LoginAsync(
        HttpContext http,
        IDbContextFactory<IdentityDb> dbFactory,
        PasswordHasher hasher,
        PasswordTries tries,
        SessionTokens sessions,
        SecurityLog log,
        IOptionsMonitor<IdentityOptions> options)
    {
        var request = await AuthJson.ReadAsync<Request>(http);
        var login = request?.Login?.Trim() ?? "";
        var password = request?.Password ?? "";
        if (login.Length == 0 || password.Length == 0)
        {
            return AuthJson.BadRequest("bad_request",
                """Send JSON like {"login": "09121234567", "password": "..."}. The login is the phone number or the username.""");
        }

        var phone = PhoneNumber.Normalize(login);
#pragma warning disable CA1308 // Usernames are compared in lower case, as the unique index does (AUTH-01).
        var name = login.ToLowerInvariant();
#pragma warning restore CA1308
        if (tries.Check(phone ?? name) is { } wait)
        {
            log.LoginFailed(SessionTokens.ByPassword, "too many tries");
            return AuthJson.TooManyRequests(http, "rate_limited", wait,
                $"Too many tries for this login. Wait {AuthJson.Seconds(wait)} second(s), or log in with an SMS code.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var user = await db.Users
#pragma warning disable CA1304, CA1311, CA1862 // This runs in PostgreSQL as lower(username), which the unique index covers.
            .Where(u => phone != null ? u.Phone == phone : u.Username != null && u.Username.ToLower() == name)
#pragma warning restore CA1304, CA1311, CA1862
            .Select(u => new { u.Id, u.PasswordHash })
            .FirstOrDefaultAsync(http.RequestAborted);

        if (!await IsRightAsync(hasher, user?.PasswordHash, password, options.CurrentValue.PasswordMaxLength))
        {
            log.LoginFailed(SessionTokens.ByPassword, "wrong login or password");
            // One answer for an unknown login, a user with no password and a wrong password.
            return AuthJson.Error(StatusCodes.Status401Unauthorized, "wrong_login",
                "The login or the password is wrong. You can also log in with an SMS code.");
        }

        await sessions.SignInAsync(http, user!.Id, SessionTokens.ByPassword);
        return AuthJson.Ok(await SessionEndpoints.ProfileAsync(dbFactory, user.Id, http.RequestAborted));
    }

    private static async Task<bool> IsRightAsync(PasswordHasher hasher, string? stored, string password, int maxLength)
    {
        if (password.Length > maxLength) return false;
        if (stored is not null) return await hasher.VerifyAsync(stored, password);

        // No such user or no password: do the same work, so the answer time does not tell which logins exist.
        await hasher.HashAsync(password);
        return false;
    }
}
