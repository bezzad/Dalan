using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dalan.Testing;
using Microsoft.AspNetCore.WebUtilities;
using static Dalan.Identity.Tests.Http;

namespace Dalan.Identity.Tests;

/// <summary>Helpers of the "Login with Dalan" tests (AUTH-40 to AUTH-43): a site with a client, the authorize step, the token step.</summary>
internal static class OAuth
{
    public const string Domain = "sites.test";
    public const string AdminPhone = "09129990001";
    public const string Issuer = "https://dalanet.ir";

    public static Task<DalanApp> StartSitesAsync(PostgresServer postgres, params (string Key, string Value)[] settings) =>
        Http.StartAsync(postgres, [
            ("Sites:Domain", Domain), ("Sites:Scheme", "https"), ("Identity:AdminPhones", AdminPhone), ("Admin:SiteUrl", Issuer),
            .. settings]);

    /// <summary>An approved builder who owns these sites. Returns the builder's browser.</summary>
    public static async Task<(HttpClient Browser, long UserId)> OwnerAsync(DalanApp app, params string[] sites)
    {
        var (admin, _) = await app.LoginAsync("Owner", AdminPhone);
        var (browser, userId) = await app.LoginAsync("Builder");
        (await admin.PostAsJsonAsync($"/api/admin/users/{userId}/builder", new { approve = true }, Cancel)).EnsureSuccessStatusCode();
        foreach (var site in sites) (await browser.PostAsJsonAsync("/api/sites", new { name = site }, Cancel)).EnsureSuccessStatusCode();
        return (browser, userId);
    }

    /// <summary>Turns Login with Dalan on for the site and returns its secret.</summary>
    public static async Task<string> TurnOnAsync(HttpClient owner, string site)
    {
        var response = await owner.PostAsJsonAsync("/api/oauth/clients", new { site }, Cancel);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await JsonAsync(response)).GetProperty("secret").GetString()!;
    }

    /// <summary>A site with Login with Dalan on: the owner's browser and the secret.</summary>
    public static async Task<(HttpClient Owner, string Secret)> ClientAsync(DalanApp app, string site = "shop")
    {
        var (owner, _) = await OwnerAsync(app, site);
        return (owner, await TurnOnAsync(owner, site));
    }

    public static string NewVerifier() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public static string Challenge(string verifier) => Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    /// <summary>The query a site sends to /oauth/authorize. A null value leaves that parameter out.</summary>
    public static string Query(
        string client = "shop", string? redirect = null, string scope = "openid profile", string? challenge = "",
        string? method = "S256", string state = "st-1", string? nonce = "n-1", string responseType = "code")
    {
        var values = new Dictionary<string, string?>
        {
            ["response_type"] = responseType,
            ["client_id"] = client,
            ["redirect_uri"] = redirect ?? $"https://{client}.{Domain}/cb",
            ["scope"] = scope,
            ["state"] = state,
        };
        if (nonce is not null) values["nonce"] = nonce;
        if (challenge is not null) values["code_challenge"] = challenge.Length == 0 ? Challenge(Verifier) : challenge;
        if (method is not null) values["code_challenge_method"] = method;
        return QueryHelpers.AddQueryString("", values);
    }

    /// <summary>The verifier of the default challenge in <see cref="Query"/>.</summary>
    public static readonly string Verifier = NewVerifier();

    /// <summary>What the consent screen posts: the query of the page, and the answer (null: only ask).</summary>
    public static async Task<HttpResponseMessage> AuthorizeAsync(
        HttpClient browser, string query, string? answer = null, bool email = false, string origin = "https://localhost")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/authorize")
        {
            Content = JsonContent.Create(new { query, answer, email }),
        };
        request.Headers.Add("Origin", origin);
        return await browser.SendAsync(request, Cancel);
    }

    /// <summary>The redirect address of an answer that sends the browser back to the site.</summary>
    public static async Task<Uri> RedirectAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.True(body.TryGetProperty("redirect", out var redirect), body.ToString());
        return new Uri(redirect.GetString()!);
    }

    public static Dictionary<string, string> Params(Uri address) =>
        QueryHelpers.ParseQuery(address.Query).ToDictionary(p => p.Key, p => p.Value.ToString());

    /// <summary>Asks, then allows: the code the site gets back.</summary>
    public static async Task<string> CodeAsync(HttpClient browser, string query, bool email = false)
    {
        var first = await AuthorizeAsync(browser, query);
        var body = await JsonAsync(first);
        var back = body.TryGetProperty("consent", out _)
            ? await RedirectAsync(await AuthorizeAsync(browser, query, "allow", email))
            : new Uri(body.GetProperty("redirect").GetString()!);
        return Params(back)["code"];
    }

    /// <summary>The site's server swaps the code (the secret by Basic auth, or in the form).</summary>
    public static Task<HttpResponseMessage> TokenAsync(
        DalanApp app, string code, string secret, string client = "shop", string? redirect = null, string? verifier = null,
        bool basic = true, string endPoint = "80.1.2.3:44000")
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirect ?? $"https://{client}.{Domain}/cb",
            ["code_verifier"] = verifier ?? Verifier,
        };
        var server = DalanApp.From(NoJar(app), endPoint);
        if (basic)
        {
            server.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Uri.EscapeDataString(client)}:{Uri.EscapeDataString(secret)}")));
        }
        else
        {
            form["client_id"] = client;
            form["client_secret"] = secret;
        }

        return server.PostAsync("/oauth/token", new FormUrlEncodedContent(form), Cancel);
    }

    public static async Task<JsonElement> TokensAsync(DalanApp app, string code, string secret, string client = "shop", string? redirect = null)
    {
        var response = await TokenAsync(app, code, secret, client, redirect);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await JsonAsync(response);
    }

    public static async Task<HttpResponseMessage> UserInfoAsync(DalanApp app, string accessToken)
    {
        var client = NoJar(app);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.GetAsync("/oauth/userinfo", Cancel);
    }

    /// <summary>The claims of a JWT without checking it (the full check is in AUTH42_code_for_tokens_full_flow).</summary>
    public static JsonElement Claims(string jwt) => JsonDocument.Parse(Base64Url.DecodeFromChars(jwt.Split('.')[1])).RootElement;
}
