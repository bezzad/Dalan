using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Dalan.Identity.Sessions;

/// <summary>AUTH-03: a session is an ES256 JWT in a cookie that JavaScript cannot read.</summary>
internal sealed class SessionTokens(SessionKey key, IOptionsMonitor<IdentityOptions> options, TimeProvider time)
{
    /// <summary>The "__Host-" prefix makes the browser refuse the cookie without Secure or with a Domain.</summary>
    public const string CookieName = "__Host-dalan_session";

    /// <summary>The claim that holds the user id.</summary>
    public const string UserIdClaim = "sub";

    public void SignIn(HttpContext http, long userId)
    {
        var now = time.GetUtcNow();
        var lifetime = TimeSpan.FromMinutes(options.CurrentValue.AccessTokenMinutes);
        var expires = now + lifetime;
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim(UserIdClaim, userId.ToString(CultureInfo.InvariantCulture))]),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(key.Key, SecurityAlgorithms.EcdsaSha256),
        });

        http.Response.Cookies.Append(CookieName, token, Cookie(lifetime));
    }

    public static void SignOut(HttpContext http) => http.Response.Cookies.Delete(CookieName, Cookie(lifetime: null));

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
            OnMessageReceived = context =>
            {
                context.Token = context.Request.Cookies[CookieName];
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                context.HandleResponse();
                return Unauthorized().ExecuteAsync(context.HttpContext);
            },
        };
    }

    public static IResult Unauthorized() => Results.Json(
        new { error = "unauthorized", message = "You are not logged in, or the session has ended. Log in again." },
        statusCode: StatusCodes.Status401Unauthorized);

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
