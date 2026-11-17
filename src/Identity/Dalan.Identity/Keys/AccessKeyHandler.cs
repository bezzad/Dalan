using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Dalan.Identity.Data;
using Dalan.Identity.Sessions;
using Dalan.Identity.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dalan.Identity.Keys;

/// <summary>
/// AUTH-22: reads "Authorization: Bearer dalan_..." and gives the key's user, like a session would.
/// Only doors that name this scheme in their policy accept it.
/// </summary>
internal sealed class AccessKeyHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IDbContextFactory<IdentityDb> dbFactory,
    SuspendedUsers suspendedUsers,
    TimeProvider time)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string Bearer = "Bearer ";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? header = Request.Headers.Authorization;
        if (header is null || !header.StartsWith(Bearer + KeyText.Prefix, StringComparison.Ordinal)) return AuthenticateResult.NoResult();

        var hash = KeyText.Hash(header[Bearer.Length..]);
        var now = time.GetUtcNow();
        await using var db = await dbFactory.CreateDbContextAsync(Context.RequestAborted);
        var userId = await db.AccessKeys.Where(k => k.KeyHash == hash && k.ExpiresAt > now)
            .Select(k => (long?)k.UserId)
            .FirstOrDefaultAsync(Context.RequestAborted);
        // AUTH-20: a suspended user's key stops at once, like the session.
        if (userId is not { } id || suspendedUsers.Contains(id)) return AuthenticateResult.Fail("unknown key");

        var identity = new ClaimsIdentity(
        [
            new Claim(SessionTokens.UserIdClaim, id.ToString(CultureInfo.InvariantCulture)),
            new Claim(SessionTokens.MethodClaim, KeyText.Method),
        ], Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    /// <summary>The session handler writes the 401 answer; a second one would write after the body.</summary>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties) => Task.CompletedTask;
}
