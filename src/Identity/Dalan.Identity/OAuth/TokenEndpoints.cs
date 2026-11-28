using System.Security.Cryptography;
using System.Text;
using Dalan.Admin.Contracts;
using Dalan.Identity.Data;
using Dalan.Identity.Users;
using Dalan.Sites.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Dalan.Identity.OAuth;

/// <summary>An error as OAuth 2.0 writes it (RFC 6749 section 5.2).</summary>
internal sealed record OAuthErrorJson(string Error, string ErrorDescription);

internal sealed record TokenJson(string AccessToken, string TokenType, int ExpiresIn, string IdToken, string Scope);

/// <summary>AUTH-42: what a library reads first. Fixed, but for the addresses.</summary>
internal sealed record DiscoveryJson(
    string Issuer, string AuthorizationEndpoint, string TokenEndpoint, string UserinfoEndpoint, string JwksUri,
    string[] ResponseTypesSupported, string[] ResponseModesSupported, string[] GrantTypesSupported, string[] SubjectTypesSupported,
    string[] IdTokenSigningAlgValuesSupported, string[] ScopesSupported, string[] TokenEndpointAuthMethodsSupported,
    string[] ClaimsSupported, string[] CodeChallengeMethodsSupported);

/// <summary>
/// AUTH-42: the token step, the ID token, user info, discovery and the keys: standard OpenID Connect (authorization code with
/// PKCE, confidential clients, no refresh token), so every library works. AUTH-43: only the claims the person agreed to.
/// </summary>
internal static class TokenEndpoints
{
    public static void MapTokens(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/oauth/token", TokenAsync);
        endpoints.MapMethods("/oauth/userinfo", [HttpMethods.Get, HttpMethods.Post], UserInfoAsync);
        endpoints.MapGet("/.well-known/openid-configuration", (IConfiguration configuration) => AuthJson.Ok(Discovery(Issuer(configuration))));
        endpoints.MapGet("/oauth/jwks", (OAuthKey key) => AuthJson.Ok(key.Jwks));
    }

    /// <summary>Dalan's own address (Admin:SiteUrl), with no slash at the end: the issuer of every ID token.</summary>
    public static string Issuer(IConfiguration configuration) => (configuration["Admin:SiteUrl"] ?? "").TrimEnd('/');

    private static DiscoveryJson Discovery(string issuer) => new(
        issuer, $"{issuer}/oauth/authorize", $"{issuer}/oauth/token", $"{issuer}/oauth/userinfo", $"{issuer}/oauth/jwks",
        ["code"], ["query"], ["authorization_code"], ["pairwise"], [SecurityAlgorithms.EcdsaSha256],
        [Scopes.OpenId, Scopes.Profile, Scopes.Email], ["client_secret_basic", "client_secret_post"],
        ["sub", "iss", "aud", "exp", "iat", "nonce", "name", "preferred_username", "email", "email_verified"], ["S256"]);

    private static async Task<IResult> TokenAsync(
        HttpContext http, OAuthStore store, OAuthKey key, ISiteDirectory sites, IAccessLog accessLog,
        IDbContextFactory<IdentityDb> dbFactory, IOptionsMonitor<IdentityOptions> options, IConfiguration configuration, TimeProvider time)
    {
        NoStore(http);
        if (!http.Request.HasFormContentType) return Error("invalid_request", "Send the form as application/x-www-form-urlencoded.");
        var form = await http.Request.ReadFormAsync(http.RequestAborted);
        if (form["grant_type"] != "authorization_code")
            return Error("unsupported_grant_type", "Only grant_type=authorization_code is supported. There are no refresh tokens.");

        // The spec (AUTH-42) answers a wrong secret like a wrong code: 400 invalid_grant.
        var (clientId, secret) = ClientOf(http, form);
        var client = clientId is null ? null : await store.FindClientAsync(clientId, http.RequestAborted);
        var site = client is null ? null : await sites.FindAsync(client.ClientId, http.RequestAborted);
        if (client is null || site is null || site.OwnerUserId != client.OwnerUserId || secret is null
            || !CryptographicOperations.FixedTimeEquals(OAuthStore.Hash(secret), client.SecretHash))
        {
            return Error("invalid_grant", "The client id or the secret is wrong, or Login with Dalan is off for this site.");
        }

        // ADM-01: the site's server is the owner's; its address is recorded.
        await accessLog.RecordAsync(AccessActor.User, client.OwnerUserId, http.Connection.RemoteIpAddress, http.Connection.RemotePort,
            http.RequestAborted);

        var code = form["code"].ToString();
        var row = code.Length == 0 ? null : await store.FindCodeAsync(code, http.RequestAborted);
        if (row is null || row.Proxy || row.ClientId != client.ClientId) return BadGrant();
        // Spent before the other checks, so a stolen code cannot be tried again with other values.
        if (!await store.SpendCodeAsync(code, http.RequestAborted)) return BadGrant();
        if (form["redirect_uri"] != row.RedirectUri || !Pkce.Matches(form["code_verifier"].ToString(), row.Challenge)) return BadGrant();

        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == row.UserId && u.SuspendedAt == null, http.RequestAborted);
        if (user is null) return BadGrant();

        var minutes = options.CurrentValue.OAuthTokenMinutes;
        var now = time.GetUtcNow();
        var claims = Claims(key, client.ClientId, user, row.Scopes, row.Email);
        if (row.Nonce is not null) claims["nonce"] = row.Nonce;
        var idToken = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer(configuration),
            Audience = client.ClientId,
            Claims = claims,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.AddMinutes(minutes).UtcDateTime,
            SigningCredentials = new SigningCredentials(key.Key, SecurityAlgorithms.EcdsaSha256),
        });
        var accessToken = await store.NewTokenAsync(code, row, http.RequestAborted);
        return AuthJson.Ok(new TokenJson(accessToken, "Bearer", minutes * 60, idToken, row.Scopes));
    }

    private static async Task<IResult> UserInfoAsync(
        HttpContext http, OAuthStore store, OAuthKey key, ISiteDirectory sites, IAccessLog accessLog, IDbContextFactory<IdentityDb> dbFactory)
    {
        NoStore(http);
        var header = http.Request.Headers.Authorization.ToString();
        var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..].Trim() : "";
        var row = token.Length == 0 ? null : await store.FindTokenAsync(token, http.RequestAborted);
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var user = row is null ? null
            : await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == row.UserId && u.SuspendedAt == null, http.RequestAborted);
        var site = row is null ? null : await sites.FindAsync(row.ClientId, http.RequestAborted);
        if (row is null || user is null || site is null)
        {
            http.Response.Headers.WWWAuthenticate = "Bearer error=\"invalid_token\"";
            return Results.Json(new OAuthErrorJson("invalid_token", "The access token is wrong or has ended. Log the person in again."),
                AuthJson.Options, statusCode: StatusCodes.Status401Unauthorized);
        }

        await accessLog.RecordAsync(AccessActor.User, site.OwnerUserId, http.Connection.RemoteIpAddress, http.Connection.RemotePort,
            http.RequestAborted);
        return Results.Json(Claims(key, row.ClientId, user, row.Scopes, row.Email), AuthJson.Options);
    }

    /// <summary>AUTH-43: the site's own user id, the name with "profile", the email only with "email" when the person ticked it.</summary>
    public static Dictionary<string, object> Claims(OAuthKey key, string clientId, User user, string scopes, bool email)
    {
        var given = scopes.Split(' ');
        var claims = new Dictionary<string, object> { ["sub"] = key.Subject(clientId, user.Id) };
        if (given.Contains(Scopes.Profile))
        {
            claims["name"] = user.DisplayName;
            if (user.Username is { } username) claims["preferred_username"] = username;
        }

        if (email && given.Contains(Scopes.Email) && user.Email is { } address)
        {
            claims["email"] = address;
            // Every email of Dalan was proved with a code or by GitHub (AUTH-24, AUTH-28).
            claims["email_verified"] = true;
        }

        return claims;
    }

    /// <summary>RFC 6749 section 2.3.1: Basic auth (each part form-encoded), or client_id and client_secret in the form.</summary>
    private static (string? ClientId, string? Secret) ClientOf(HttpContext http, IFormCollection form)
    {
        var header = http.Request.Headers.Authorization.ToString();
        if (header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var pair = Encoding.UTF8.GetString(Convert.FromBase64String(header[6..].Trim()));
                var colon = pair.IndexOf(':', StringComparison.Ordinal);
                return colon < 0 ? (null, null) : (Uri.UnescapeDataString(pair[..colon]), Uri.UnescapeDataString(pair[(colon + 1)..]));
            }
            catch (FormatException)
            {
                return (null, null);
            }
        }

        return (NullIfEmpty(form["client_id"].ToString()), NullIfEmpty(form["client_secret"].ToString()));
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    private static IResult BadGrant() =>
        Error("invalid_grant", "The code is wrong, used or old, or redirect_uri or code_verifier does not match. Log the person in again.");

    private static IResult Error(string code, string message) =>
        Results.Json(new OAuthErrorJson(code, message), AuthJson.Options, statusCode: StatusCodes.Status400BadRequest);

    private static void NoStore(HttpContext http)
    {
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers.Pragma = "no-cache";
    }
}

/// <summary>RFC 7636: the verifier is 43 to 128 characters, and its base64url SHA-256 is the challenge.</summary>
internal static class Pkce
{
    public static bool Matches(string verifier, string challenge)
    {
        if (verifier.Length is < 43 or > 128 || !verifier.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or '~')) return false;
        var made = System.Buffers.Text.Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(made), Encoding.ASCII.GetBytes(challenge));
    }
}
