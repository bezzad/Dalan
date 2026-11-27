using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Dalan.Admin.Contracts;
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
    IAccessLog accessLog,
    TimeProvider time)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string Bearer = "Bearer ";

    /// <summary>AUTH-38: set on the call when a right key came from an address not in its list; the answer is then 403.</summary>
    public const string AddressRefused = "dalan.key_address_refused";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? header = Request.Headers.Authorization;
        if (header is null || !header.StartsWith(Bearer + KeyText.Prefix, StringComparison.Ordinal)) return AuthenticateResult.NoResult();

        var hash = KeyText.Hash(header[Bearer.Length..]);
        var now = time.GetUtcNow();
        await using var db = await dbFactory.CreateDbContextAsync(Context.RequestAborted);
        var key = await db.AccessKeys.Where(k => k.KeyHash == hash && k.ExpiresAt > now)
            .Select(k => new { k.UserId, k.Addresses })
            .FirstOrDefaultAsync(Context.RequestAborted);
        // AUTH-20: a suspended user's key stops at once, like the session.
        if (key is null || suspendedUsers.Contains(key.UserId)) return AuthenticateResult.Fail("unknown key");
        var id = key.UserId;

        // AUTH-38: a key with an address list works only from those addresses. The refused call is in the access log (rule 13).
        var address = SessionTokens.Address(Context);
        if (!KeyAddresses.Allows(key.Addresses, address))
        {
            await accessLog.RecordAsync(AccessActor.User, id, address, Context.Connection.RemotePort, Context.RequestAborted);
            Context.Items[AddressRefused] = true;
            return AuthenticateResult.Fail("key_address_refused");
        }

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
