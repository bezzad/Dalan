using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dalan.Identity.Contracts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Dalan.Sites.Login;

/// <summary>The login of one visitor on one site, inside the login cookie. <see cref="Expires"/> is Unix milliseconds.</summary>
internal sealed record LoginTicket(string Site, string Subject, string Name, long Expires);

/// <summary>What waits in the state cookie while the visitor is on Dalan's login page.</summary>
internal sealed record StateTicket(string Site, string State, string Verifier, string Return, long Expires);

/// <summary>
/// SITE-26: the login on the proxy. The proxy is Dalan's own client of the site (AUTH-41 with PKCE, no secret: it swaps the code
/// inside the process through <see cref="IProxyLogin"/>). Both cookies are host-only (the "__Host-" prefix), HttpOnly, Secure,
/// SameSite=Lax, and protected (encrypted and signed) by ASP.NET Core Data Protection, with the site name and the end time inside.
/// </summary>
internal sealed class LoginGate(
    IDataProtectionProvider protection, IProxyLogin proxyLogin, IOptionsMonitor<SitesOptions> options, IConfiguration configuration,
    TimeProvider time)
{
    public const string Cookie = "__Host-dalan_login";
    public const string StateCookie = "__Host-dalan_login_state";

    /// <summary>The proxy's own paths on every site host. They never reach the site.</summary>
    public const string Prefix = "/.dalan";

    public const string CallbackPath = "/.dalan/callback";

    private readonly IDataProtector _login = protection.CreateProtector("Sites.Login");
    private readonly IDataProtector _state = protection.CreateProtector("Sites.LoginState");

    /// <summary>The visitor's login on this site, or null.</summary>
    public LoginTicket? Read(HttpContext http, string site)
    {
        var ticket = Unprotect<LoginTicket>(_login, http.Request.Cookies[Cookie]);
        return ticket is not null && ticket.Site == site && ticket.Expires > time.GetUtcNow().ToUnixTimeMilliseconds() ? ticket : null;
    }

    /// <summary>Sends the visitor to Dalan's authorize step, and keeps the state, the PKCE verifier and the path to come back to.</summary>
    public void Challenge(HttpContext http, string site)
    {
        var settings = options.CurrentValue;
        var state = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var verifier = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var path = http.Request.Path.Value ?? "/";
        // Only a path of this host: "//host" or "/\host" would leave it.
        var back = path.StartsWith("//", StringComparison.Ordinal) || path.StartsWith("/\\", StringComparison.Ordinal)
            ? "/"
            : path + http.Request.QueryString;
        var lifetime = TimeSpan.FromMinutes(settings.LoginStateMinutes);
        var ticket = new StateTicket(site, state, verifier, back, time.GetUtcNow().Add(lifetime).ToUnixTimeMilliseconds());
        http.Response.Cookies.Append(StateCookie, Protect(_state, ticket), CookieOptions(lifetime));

        var issuer = (configuration["Admin:SiteUrl"] ?? "").TrimEnd('/');
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Redirect(QueryHelpers.AddQueryString($"{issuer}/oauth/authorize", new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = site,
            ["redirect_uri"] = settings.UrlOf(site) + CallbackPath,
            ["scope"] = "openid profile",
            ["state"] = state,
            ["code_challenge"] = Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))),
            ["code_challenge_method"] = "S256",
        }));
    }

    /// <summary>
    /// The way back from Dalan: checks the state, swaps the code and sets the login cookie. Returns the path to go to, or the
    /// error code of the page to show.
    /// </summary>
    public async Task<(string? Return, string? Error)> CallbackAsync(HttpContext http, string site)
    {
        var settings = options.CurrentValue;
        var ticket = Unprotect<StateTicket>(_state, http.Request.Cookies[StateCookie]);
        http.Response.Cookies.Delete(StateCookie, CookieOptions(lifetime: null));
        var query = http.Request.Query;
        if (ticket is null || ticket.Site != site || ticket.Expires <= time.GetUtcNow().ToUnixTimeMilliseconds()
            || !Same(ticket.State, query["state"].ToString()))
        {
            return (null, "login_failed");
        }

        if (query["error"].ToString() == "access_denied") return (null, "login_denied");
        var user = await proxyLogin.RedeemAsync(site, query["code"].ToString(), ticket.Verifier, settings.UrlOf(site) + CallbackPath,
            http.Connection.RemoteIpAddress, http.Connection.RemotePort, http.RequestAborted);
        if (user is null) return (null, "login_failed");

        var lifetime = TimeSpan.FromHours(settings.LoginCookieHours);
        var login = new LoginTicket(site, user.Subject, user.Name, time.GetUtcNow().Add(lifetime).ToUnixTimeMilliseconds());
        http.Response.Cookies.Append(Cookie, Protect(_login, login), CookieOptions(lifetime));
        return (ticket.Return, null);
    }

    public static void Logout(HttpContext http) => http.Response.Cookies.Delete(Cookie, CookieOptions(lifetime: null));

    private static CookieOptions CookieOptions(TimeSpan? lifetime) => new()
    {
        HttpOnly = true,
        Secure = true,
        // Lax: the way back from Dalan is a navigation from another site.
        SameSite = SameSiteMode.Lax,
        Path = "/",
        MaxAge = lifetime,
    };

    private static bool Same(string expected, string given) =>
        given.Length > 0 && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(given));

    private static string Protect<T>(IDataProtector protector, T value) =>
        protector.Protect(JsonSerializer.Serialize(value, SitesJson.Options));

    private static T? Unprotect<T>(IDataProtector protector, string? text) where T : class
    {
        if (string.IsNullOrEmpty(text)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(protector.Unprotect(text), SitesJson.Options);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or JsonException)
        {
            return null;
        }
    }
}
