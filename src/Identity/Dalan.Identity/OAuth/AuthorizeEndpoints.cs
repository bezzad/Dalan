using System.Text.RegularExpressions;
using Dalan.Admin.Contracts;
using Dalan.Identity.Data;
using Dalan.Identity.Sessions;
using Dalan.Sites.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace Dalan.Identity.OAuth;

/// <summary>What the consent screen posts: the query of its own address as the site sent it, and the person's answer.</summary>
internal sealed record AuthorizeRequest(string? Query, string? Answer, bool Email);

/// <summary>AUTH-41: what the consent screen shows. The phone is never here (AUTH-43).</summary>
internal sealed record ConsentJson(string ClientId, string Url, string Name, string? Username, string? Email, string[] Scopes, bool AskEmail);

internal sealed record ConsentAnswer(ConsentJson Consent);

/// <summary>The page goes to this address: back to the site, with a code or an error.</summary>
internal sealed record RedirectAnswer(string Redirect);

/// <summary>
/// AUTH-41: the authorize step. <c>GET /oauth/authorize</c> is Dalan's own page (the client draws the login and the consent);
/// the page posts the query here. The session cookie is SameSite=Strict and the Origin must be this host's, so another site
/// cannot post it. A bad client or return address is an error on Dalan's page; any other error goes back to the site.
/// </summary>
internal static partial class AuthorizeEndpoints
{
    public static void MapAuthorize(this IEndpointRouteBuilder endpoints) => endpoints.MapPost("/oauth/authorize", AuthorizeAsync);

    private static async Task<IResult> AuthorizeAsync(
        HttpContext http, ISiteDirectory sites, OAuthStore store, IAccessLog accessLog, IDbContextFactory<IdentityDb> dbFactory)
    {
        if (!SameOrigin(http))
            return AuthJson.Error(StatusCodes.Status403Forbidden, "bad_origin", "This call must come from Dalan's own page.");
        if (await AuthJson.ReadAsync<AuthorizeRequest>(http) is not { Query: { } text } body)
            return AuthJson.BadRequest("bad_request", "Send {\"query\":\"<the query of /oauth/authorize>\",\"answer\":null|\"allow\"|\"deny\"}.");

        var query = QueryHelpers.ParseQuery(text);
        var clientId = One(query, "client_id");
        var redirect = One(query, "redirect_uri");
        var site = clientId is null ? null : await sites.FindAsync(clientId, http.RequestAborted);
        var client = site is null ? null : await store.FindClientAsync(clientId!, http.RequestAborted);
        var clientOn = client is not null && client.OwnerUserId == site!.OwnerUserId;
        if (site is null || (!clientOn && !site.ProxyLogin)) return BadClient();

        var kind = ReturnAddress.Check(redirect, site.Url);
        if (kind == ReturnKind.Bad || (kind == ReturnKind.Proxy && !site.ProxyLogin))
            return AuthJson.BadRequest("invalid_redirect_uri",
                $"This login may return only to {site.Url}/... or http://localhost. The site must send another redirect_uri.");
        if (kind != ReturnKind.Proxy && !clientOn) return BadClient();

        // From here the return address is the site's own, so errors go back to it (RFC 6749 section 4.1.2.1).
        var state = One(query, "state");
        var scopes = (One(query, "scope") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var challenge = One(query, "code_challenge");
        if (One(query, "response_type") != "code") return Back(redirect!, state, "unsupported_response_type");
        if (!scopes.Contains(Scopes.OpenId)) return Back(redirect!, state, "invalid_scope");
        if (One(query, "code_challenge_method") != "S256" || challenge is null || !ChallengePattern().IsMatch(challenge))
            return Back(redirect!, state, "invalid_request");

        if (!(http.User.Identity?.IsAuthenticated ?? false))
            return AuthJson.Error(StatusCodes.Status401Unauthorized, "login_required", "Log in to Dalan first.");
        var userId = SessionTokens.UserId(http.User);
        await accessLog.RecordAsync(AccessActor.User, userId, http.Connection.RemoteIpAddress, http.Connection.RemotePort, http.RequestAborted);

        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, http.RequestAborted);
        if (user is null) return AuthJson.Unauthorized();

        var granted = Scopes.Known(scopes);
        var askEmail = granted.Contains(Scopes.Email) && user.Email is not null;
        bool email;
        switch (body.Answer)
        {
            case null:
                var consent = await store.FindConsentAsync(userId, clientId!, http.RequestAborted);
                if (consent is null || consent.OwnerUserId != site.OwnerUserId || !Scopes.Covers(consent.Scopes, granted))
                    return AuthJson.Ok(new ConsentAnswer(new ConsentJson(clientId!, site.Url, user.DisplayName, user.Username,
                        user.Email, granted, askEmail)));
                email = consent.Email && askEmail;
                break;
            case "deny":
                return Back(redirect!, state, "access_denied");
            case "allow":
                email = body.Email && askEmail;
                await store.SaveConsentAsync(userId, clientId!, site.OwnerUserId, string.Join(' ', granted), email, http.RequestAborted);
                break;
            default:
                return AuthJson.BadRequest("bad_request", "The answer must be \"allow\", \"deny\" or null.");
        }

        var code = await store.NewCodeAsync(clientId!, userId, redirect!, challenge, One(query, "nonce"), string.Join(' ', granted), email,
            kind == ReturnKind.Proxy, http.RequestAborted);
        return AuthJson.Ok(new RedirectAnswer(QueryHelpers.AddQueryString(redirect!, Without(new() { ["code"] = code, ["state"] = state }))));
    }

    private static IResult BadClient() => AuthJson.BadRequest("invalid_client",
        "This site has not turned on Login with Dalan, or it is not a site of Dalan. Its owner can turn it on in My apps.");

    private static IResult Back(string redirect, string? state, string error) =>
        AuthJson.Ok(new RedirectAnswer(QueryHelpers.AddQueryString(redirect, Without(new() { ["error"] = error, ["state"] = state }))));

    private static Dictionary<string, string?> Without(Dictionary<string, string?> values) =>
        values.Where(pair => pair.Value is not null).ToDictionary();

    /// <summary>One value; a parameter that is missing, empty or given twice counts as missing.</summary>
    private static string? One(Dictionary<string, StringValues> query, string name) =>
        query.TryGetValue(name, out var values) && values.Count == 1 && !string.IsNullOrEmpty(values[0]) ? values[0] : null;

    /// <summary>The page that posts is on this host. Not the scheme: behind the front proxy the server itself talks plain HTTP.</summary>
    private static bool SameOrigin(HttpContext http) =>
        Uri.TryCreate(http.Request.Headers.Origin.ToString(), UriKind.Absolute, out var origin)
        && string.Equals(origin.Authority, http.Request.Host.Value, StringComparison.OrdinalIgnoreCase);

    /// <summary>RFC 7636: a base64url SHA-256 is 43 characters.</summary>
    [GeneratedRegex("^[A-Za-z0-9_-]{43}$")]
    private static partial Regex ChallengePattern();
}

/// <summary>AUTH-42, AUTH-43: the scopes Dalan knows; any other is left out, as OpenID Connect says.</summary>
internal static class Scopes
{
    public const string OpenId = "openid";
    public const string Profile = "profile";
    public const string Email = "email";

    private static readonly string[] All = [OpenId, Profile, Email];

    public static string[] Known(IEnumerable<string> asked) => [.. All.Where(asked.Contains)];

    public static bool Covers(string given, IEnumerable<string> asked)
    {
        var have = given.Split(' ');
        return asked.All(have.Contains);
    }
}
