using System.Buffers.Text;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using Dalan.Identity.Data;
using Dalan.Identity.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Dalan.Identity.Sessions;

/// <summary>
/// AUTH-03: a session is an ES256 JWT in a cookie that JavaScript cannot read.
/// AUTH-08 to AUTH-10: a refresh token in a second cookie renews it, and is replaced on every use.
/// </summary>
internal sealed class SessionTokens(
    SessionKey key,
    IOptionsMonitor<IdentityOptions> options,
    TimeProvider time,
    IDbContextFactory<IdentityDb> dbFactory,
    SuspendedUsers suspendedUsers,
    SecurityLog log)
{
    /// <summary>The "__Host-" prefix makes the browser refuse the cookie without Secure or with a Domain.</summary>
    public const string CookieName = "__Host-dalan_session";

    public const string RefreshCookieName = "__Host-dalan_refresh";

    /// <summary>The claim that holds the user id.</summary>
    public const string UserIdClaim = "sub";

    /// <summary>The claim that says how the session started: "sms", "email", "github", "pwd" or "dev".</summary>
    public const string MethodClaim = "amr";

    public const string BySms = "sms";
    public const string ByEmail = "email";
    public const string ByGitHub = "github";
    public const string ByPassword = "pwd";
    public const string ByDevLogin = "dev";

    /// <summary>Starts a session on this device: one row, two cookies. Every kind of login ends here.</summary>
    public async Task SignInAsync(HttpContext http, long userId, string method)
    {
        var now = time.GetUtcNow();
        var refreshToken = NewRefreshToken();
        var replaced = RefreshHash(http);

        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        // Rows of no more use: the user's ended sessions, and the session this browser had until now.
        await db.Sessions
            .Where(s => (s.UserId == userId && s.ExpiresAt <= now) || (replaced != null && s.TokenHash == replaced))
            .ExecuteDeleteAsync(http.RequestAborted);
        db.Sessions.Add(new Session
        {
            UserId = userId,
            TokenHash = Hash(refreshToken),
            Method = method,
            CreatedAt = now,
            RotatedAt = now,
            ExpiresAt = now + RefreshLifetime,
        });
        await db.SaveChangesAsync(http.RequestAborted);

        SetCookies(http, userId, method, refreshToken);
        log.Login(userId, method);
    }

    /// <summary>AUTH-09: swaps the refresh token for a new one and gives a new session token. False: log in again.</summary>
    public async Task<bool> RefreshAsync(HttpContext http)
    {
        if (RefreshHash(http) is not { } current) return false;

        var now = time.GetUtcNow();
        var expires = now + RefreshLifetime;
        var refreshToken = NewRefreshToken();
        var next = Hash(refreshToken);

        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        // One statement, so of two calls with the same token only one can win.
        var swapped = await db.Sessions
            .Where(s => s.TokenHash == current && s.ExpiresAt > now)
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.PreviousHash, s => s.TokenHash)
                .SetProperty(s => s.TokenHash, next)
                .SetProperty(s => s.RotatedAt, now)
                .SetProperty(s => s.ExpiresAt, expires), http.RequestAborted);

        if (swapped == 0)
        {
            // AUTH-10: a token that was already replaced. After the grace time it means a copy exists: end the session.
            var grace = now.AddSeconds(-options.CurrentValue.RefreshReuseGraceSeconds);
            var ended = await db.Sessions
                .Where(s => s.PreviousHash == current && s.RotatedAt <= grace)
                .ExecuteDeleteAsync(http.RequestAborted);
            if (ended > 0) log.RefreshReuse();
            // The cookies stay as they are: another tab may have just received new ones.
            return false;
        }

        var session = await db.Sessions.Where(s => s.TokenHash == next)
            .Select(s => new { s.UserId, s.Method })
            .FirstAsync(http.RequestAborted);
        SetCookies(http, session.UserId, session.Method, refreshToken);
        return true;
    }

    /// <summary>AUTH-05: ends this device's session and removes both cookies.</summary>
    public async Task SignOutAsync(HttpContext http)
    {
        if (RefreshHash(http) is { } current)
        {
            await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
            await db.Sessions.Where(s => s.TokenHash == current).ExecuteDeleteAsync(http.RequestAborted);
        }

        http.Response.Cookies.Delete(CookieName, Cookie(lifetime: null));
        http.Response.Cookies.Delete(RefreshCookieName, Cookie(lifetime: null));
    }

    /// <summary>AUTH-13: ends every session of the user except the one of this browser.</summary>
    public async Task SignOutOtherDevicesAsync(HttpContext http, long userId)
    {
        var current = RefreshHash(http) ?? [];
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        await db.Sessions.Where(s => s.UserId == userId && s.TokenHash != current).ExecuteDeleteAsync(http.RequestAborted);
    }

    /// <summary>AUTH-31: the session row of this browser's user, or null when it sent no refresh token or the session ended.</summary>
    public async Task<long?> SessionIdAsync(HttpContext http, long userId)
    {
        if (RefreshHash(http) is not { } current) return null;
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        return await db.Sessions.Where(s => s.TokenHash == current && s.UserId == userId)
            .Select(s => (long?)s.Id).FirstOrDefaultAsync(http.RequestAborted);
    }

    public static long UserId(ClaimsPrincipal user) =>
        long.Parse(user.FindFirstValue(UserIdClaim)!, CultureInfo.InvariantCulture);

    /// <summary>Tells the standard JWT handler where the token is and how to check it.</summary>
    public void Configure(JwtBearerOptions jwt)
    {
        jwt.MapInboundClaims = false;
        jwt.TokenValidationParameters = new TokenValidationParameters
        {
            IssuerSigningKey = key.Key,
            ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
            // One system signs and reads the token, so issuer and audience say nothing.
            ValidateIssuer = false,
            ValidateAudience = false,
            RequireExpirationTime = true,
            // The injected clock decides, so tests can move time.
            LifetimeValidator = (notBefore, expires, _, _) =>
            {
                var now = time.GetUtcNow().UtcDateTime;
                return notBefore <= now && now < expires;
            },
        };
        jwt.Events = new JwtBearerEvents
        {
            // AUTH-22: only the cookie holds a session, never the Authorization header (that is for access keys).
            OnMessageReceived = context =>
            {
                context.Token = context.Request.Cookies[CookieName];
                if (string.IsNullOrEmpty(context.Token)) context.NoResult();
                return Task.CompletedTask;
            },
            // AUTH-20: a suspended user's token stops at once, before it expires.
            OnTokenValidated = context =>
            {
                if (context.Principal is { } principal && suspendedUsers.Contains(UserId(principal))) context.Fail("suspended");
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                context.HandleResponse();
                return AuthJson.Unauthorized().ExecuteAsync(context.HttpContext);
            },
        };
    }

    private TimeSpan RefreshLifetime => TimeSpan.FromDays(options.CurrentValue.RefreshTokenDays);

    private void SetCookies(HttpContext http, long userId, string method, string refreshToken)
    {
        var now = time.GetUtcNow();
        var lifetime = TimeSpan.FromMinutes(options.CurrentValue.AccessTokenMinutes);
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(UserIdClaim, userId.ToString(CultureInfo.InvariantCulture)),
                new Claim(MethodClaim, method),
            ]),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = (now + lifetime).UtcDateTime,
            SigningCredentials = new SigningCredentials(key.Key, SecurityAlgorithms.EcdsaSha256),
        });

        http.Response.Cookies.Append(CookieName, token, Cookie(lifetime));
        http.Response.Cookies.Append(RefreshCookieName, refreshToken, Cookie(RefreshLifetime));
    }

    /// <summary>256 random bits. Long and random, so a fast hash is enough to store it (like a bot token).</summary>
    private static string NewRefreshToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    private static byte[] Hash(string refreshToken) => SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(refreshToken));

    /// <summary>The hash of the refresh token this browser sent, or null when it sent none.</summary>
    private static byte[]? RefreshHash(HttpContext http) =>
        http.Request.Cookies[RefreshCookieName] is { Length: > 0 } token ? Hash(token) : null;

    private static CookieOptions Cookie(TimeSpan? lifetime) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        // A length, not a date: it does not depend on the clock of the user's device.
        MaxAge = lifetime,
    };
}
