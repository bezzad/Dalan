using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using static Dalan.Identity.Tests.Http;
using static Dalan.Identity.Tests.OAuth;

namespace Dalan.Identity.Tests;

/// <summary>AUTH-41 to AUTH-43: authorize, consent, token, ID token, user info, discovery, privacy.</summary>
public class OAuthFlowTests(PostgresServer postgres)
{
    // ---- AUTH-41 authorize ----

    [Fact]
    [Trait("Rule", "AUTH-41")]
    public async Task AUTH41_first_login_shows_the_consent_and_allow_returns_a_code_and_the_state()
    {
        await using var app = await StartSitesAsync(postgres);
        await ClientAsync(app);
        var (browser, _) = await app.LoginAsync("Sara");

        var ask = await AuthorizeAsync(browser, Query(state: "xyz 1"));
        var consent = (await JsonAsync(ask)).GetProperty("consent");
        var allow = await RedirectAsync(await AuthorizeAsync(browser, Query(state: "xyz 1"), "allow"));

        Assert.Equal(HttpStatusCode.OK, ask.StatusCode);
        Assert.Equal("shop", consent.GetProperty("client_id").GetString());
        Assert.Equal("https://shop.sites.test", consent.GetProperty("url").GetString());
        Assert.Equal("Sara", consent.GetProperty("name").GetString());
        Assert.False(consent.GetProperty("ask_email").GetBoolean());
        Assert.Equal("https://shop.sites.test/cb", allow.GetLeftPart(UriPartial.Path));
        Assert.Equal("xyz 1", Params(allow)["state"]);
        Assert.Matches("^[A-Za-z0-9_-]{43}$", Params(allow)["code"]);
    }

    [Fact]
    [Trait("Rule", "AUTH-41")]
    public async Task AUTH41_the_page_itself_is_dalans_client_screen()
    {
        await using var app = await StartSitesAsync(postgres);

        var response = await app.Browser().GetAsync("/oauth/authorize" + Query(), Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    [Trait("Rule", "AUTH-41")]
    public async Task AUTH41_the_second_time_there_is_no_consent_screen()
    {
        await using var app = await StartSitesAsync(postgres);
        await ClientAsync(app);
        var (browser, _) = await app.LoginAsync();
        await CodeAsync(browser, Query());

        var again = await RedirectAsync(await AuthorizeAsync(browser, Query(state: "two")));

        Assert.Equal("two", Params(again)["state"]);
        Assert.True(Params(again).ContainsKey("code"));
    }

    [Fact]
    [Trait("Rule", "AUTH-41")]
    public async Task AUTH41_no_session_asks_for_the_login()
    {
        await using var app = await StartSitesAsync(postgres);
        await ClientAsync(app);

        var response = await AuthorizeAsync(app.Browser(), Query());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("login_required", await ErrorCodeAsync(response));
    }

    [Theory]
    [Trait("Rule", "AUTH-41")]
    [InlineData(null, "S256", "invalid_request")]
    [InlineData("", null, "invalid_request")]
    [InlineData("", "plain", "invalid_request")]
    [InlineData("short", "S256", "invalid_request")]
    public async Task AUTH41_no_pkce_returns_to_the_site_with_invalid_request(string? challenge, string? method, string error)
    {
        await using var app = await StartSitesAsync(postgres);
        await ClientAsync(app);
        var (browser, _) = await app.LoginAsync();

        var back = await RedirectAsync(await AuthorizeAsync(browser, Query(challenge: challenge, method: method)));

        Assert.Equal("https://shop.sites.test/cb", back.GetLeftPart(UriPartial.Path));
        Assert.Equal(error, Params(back)["error"]);
        Assert.Equal("st-1", Params(back)["state"]);
        Assert.False(Params(back).ContainsKey("code"));
    }

    [Fact]
    [Trait("Rule", "AUTH-41")]
    public async Task AUTH41_without_openid_or_with_another_response_type_returns_an_error_to_the_site()
    {
        await using var app = await StartSitesAsync(postgres);
        await ClientAsync(app);
        var (browser, _) = await app.LoginAsync();

        var noOpenId = await RedirectAsync(await AuthorizeAsync(browser, Query(scope: "profile")));
        var token = await RedirectAsync(await AuthorizeAsync(browser, Query(responseType: "token")));

        Assert.Equal("invalid_scope", Params(noOpenId)["error"]);
        Assert.Equal("unsupported_response_type", Params(token)["error"]);
    }

    [Fact]
    [Trait("Rule", "AUTH-41")]
    public async Task AUTH41_deny_returns_access_denied_and_keeps_no_consent()
    {
        await using var app = await StartSitesAsync(postgres);
        await ClientAsync(app);
        var (browser, _) = await app.LoginAsync();

        var back = await RedirectAsync(await AuthorizeAsync(browser, Query(), "deny"));
        var again = await JsonAsync(await AuthorizeAsync(browser, Query()));

        Assert.Equal("access_denied", Params(back)["error"]);
        Assert.Equal("st-1", Params(back)["state"]);
        Assert.True(again.TryGetProperty("consent", out _));
    }

    [Fact]
    [Trait("Rule", "AUTH-41")]
    public async Task AUTH41_a_post_from_another_origin_is_refused()
    {
        await using var app = await StartSitesAsync(postgres);
        await ClientAsync(app);
        var (browser, _) = await app.LoginAsync();

        var response = await AuthorizeAsync(browser, Query(), "allow", origin: "https://shop.sites.test");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("bad_origin", await ErrorCodeAsync(response));
        Assert.Equal(0, await PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT count(*) FROM identity.oauth_codes"));
    }

    [Fact]
    [Trait("Rule", "AUTH-41")]
    public async Task AUTH41_a_code_ends_after_its_time()
    {
        await using var app = await StartSitesAsync(postgres);
        var (_, secret) = await ClientAsync(app);
        var (browser, _) = await app.LoginAsync();
        var code = await CodeAsync(browser, Query());

        app.Time.Advance(TimeSpan.FromSeconds(61));
        var response = await TokenAsync(app, code, secret);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_grant", await ErrorCodeAsync(response));
    }

    // ---- AUTH-42 token, user info, discovery ----

    [Fact]
    [Trait("Rule", "AUTH-42")]
    public async Task AUTH42_code_for_tokens_full_flow_checked_with_a_real_library()
    {
        await using var app = await StartSitesAsync(postgres);
        var (_, secret) = await ClientAsync(app);
        var (browser, _) = await app.LoginAsync("Sara");
        var code = await CodeAsync(browser, Query(nonce: "n-42"));
        var site = NoJar(app);

        // What any OpenID Connect library does: discovery, then the keys, then the code.
        var discovery = await site.GetFromJsonAsync<JsonElement>("/.well-known/openid-configuration", Cancel);
        var jwksPath = new Uri(discovery.GetProperty("jwks_uri").GetString()!).PathAndQuery;
        var keys = new JsonWebKeySet(await site.GetStringAsync(jwksPath, Cancel));
        var tokens = await TokensAsync(app, code, secret);
        var check = await new JsonWebTokenHandler().ValidateTokenAsync(tokens.GetProperty("id_token").GetString(), new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidAudience = "shop",
            IssuerSigningKeys = keys.GetSigningKeys(),
            ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
            // The test clock is not the machine's clock.
            ValidateLifetime = false,
        });
        var info = await UserInfoAsync(app, tokens.GetProperty("access_token").GetString()!);
        var user = await JsonAsync(info);

        Assert.True(check.IsValid, check.Exception?.Message);
        Assert.Equal(Issuer, discovery.GetProperty("issuer").GetString());
        Assert.Equal("ES256", ((JsonWebToken)check.SecurityToken).Alg);
        Assert.Equal("n-42", check.Claims["nonce"]);
        Assert.Equal("Sara", check.Claims["name"]);
        Assert.Equal("Bearer", tokens.GetProperty("token_type").GetString());
        Assert.Equal(600, tokens.GetProperty("expires_in").GetInt32());
        var idToken = (JsonWebToken)check.SecurityToken;
        Assert.Equal(app.Time.GetUtcNow().AddMinutes(10).ToUnixTimeSeconds(), idToken.GetPayloadValue<long>("exp"));
        Assert.Equal(HttpStatusCode.OK, info.StatusCode);
        Assert.Equal(check.Claims["sub"], user.GetProperty("sub").GetString());
        Assert.Equal("Sara", user.GetProperty("name").GetString());
        Assert.Equal("no-store", info.Headers.CacheControl?.ToString());
    }

    [Fact]
    [Trait("Rule", "AUTH-42")]
    public async Task AUTH42_discovery_and_keys_describe_the_server()
    {
        await using var app = await StartSitesAsync(postgres);
        var site = NoJar(app);

        var discovery = await site.GetFromJsonAsync<JsonElement>("/.well-known/openid-configuration", Cancel);
        var jwks = await site.GetFromJsonAsync<JsonElement>("/oauth/jwks", Cancel);

        Assert.Equal($"{Issuer}/oauth/authorize", discovery.GetProperty("authorization_endpoint").GetString());
        Assert.Equal($"{Issuer}/oauth/token", discovery.GetProperty("token_endpoint").GetString());
        Assert.Equal($"{Issuer}/oauth/userinfo", discovery.GetProperty("userinfo_endpoint").GetString());
        Assert.Equal($"{Issuer}/oauth/jwks", discovery.GetProperty("jwks_uri").GetString());
        Assert.Equal(["code"], discovery.GetProperty("response_types_supported").EnumerateArray().Select(v => v.GetString()));
        Assert.Equal(["pairwise"], discovery.GetProperty("subject_types_supported").EnumerateArray().Select(v => v.GetString()));
        Assert.Equal(["ES256"], discovery.GetProperty("id_token_signing_alg_values_supported").EnumerateArray().Select(v => v.GetString()));
        Assert.Equal(["S256"], discovery.GetProperty("code_challenge_methods_supported").EnumerateArray().Select(v => v.GetString()));
        Assert.Contains("client_secret_basic", discovery.GetProperty("token_endpoint_auth_methods_supported").EnumerateArray().Select(v => v.GetString()));
        Assert.Contains("client_secret_post", discovery.GetProperty("token_endpoint_auth_methods_supported").EnumerateArray().Select(v => v.GetString()));
        var key = Assert.Single(jwks.GetProperty("keys").EnumerateArray());
        Assert.Equal("EC", key.GetProperty("kty").GetString());
        Assert.Equal("P-256", key.GetProperty("crv").GetString());
        Assert.Equal("ES256", key.GetProperty("alg").GetString());
        Assert.False(string.IsNullOrEmpty(key.GetProperty("kid").GetString()));
        Assert.False(key.TryGetProperty("d", out _), "the private part must never be shown");
    }

    [Fact]
    [Trait("Rule", "AUTH-42")]
    public async Task AUTH42_the_key_from_the_settings_signs_and_its_kid_is_in_the_header()
    {
        using var ec = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        await using var app = await StartSitesAsync(postgres, ("Identity:OAuthSigningKey", ec.ExportPkcs8PrivateKeyPem()));
        var (_, secret) = await ClientAsync(app);
        var (browser, _) = await app.LoginAsync();

        var tokens = await TokensAsync(app, await CodeAsync(browser, Query()), secret);
        var token = new JsonWebToken(tokens.GetProperty("id_token").GetString());
        var jwks = await NoJar(app).GetFromJsonAsync<JsonElement>("/oauth/jwks", Cancel);
        var parameters = ec.ExportParameters(includePrivateParameters: false);

        Assert.Equal(jwks.GetProperty("keys")[0].GetProperty("kid").GetString(), token.Kid);
        Assert.Equal(Base64UrlEncoder.Encode(parameters.Q.X), jwks.GetProperty("keys")[0].GetProperty("x").GetString());
    }

    [Fact]
    [Trait("Rule", "AUTH-42")]
    public async Task AUTH42_the_secret_works_in_the_form_too()
    {
        await using var app = await StartSitesAsync(postgres);
        var (_, secret) = await ClientAsync(app);
        var (browser, _) = await app.LoginAsync();

        var response = await TokenAsync(app, await CodeAsync(browser, Query()), secret, basic: false);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    [Trait("Rule", "AUTH-42")]
    public async Task AUTH42_code_used_twice_is_invalid_grant_and_ends_the_first_tokens()
    {
        await using var app = await StartSitesAsync(postgres);
        var (_, secret) = await ClientAsync(app);
        var (browser, _) = await app.LoginAsync();
        var code = await CodeAsync(browser, Query());
        var first = await TokensAsync(app, code, secret);

        var second = await TokenAsync(app, code, secret);
        var info = await UserInfoAsync(app, first.GetProperty("access_token").GetString()!);

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal("invalid_grant", await ErrorCodeAsync(second));
        Assert.Equal(HttpStatusCode.Unauthorized, info.StatusCode);
    }

    [Theory]
    [Trait("Rule", "AUTH-42")]
    [InlineData("verifier")]
    [InlineData("secret")]
    [InlineData("redirect")]
    [InlineData("client")]
    [InlineData("code")]
    public async Task AUTH42_a_wrong_part_is_invalid_grant(string wrong)
    {
        await using var app = await StartSitesAsync(postgres);
        var (_, secret) = await ClientAsync(app);
        var (owner, _) = await OwnerAsync(app, "blog");
        var blogSecret = await TurnOnAsync(owner, "blog");
        var (browser, _) = await app.LoginAsync();
        var code = await CodeAsync(browser, Query());

        var response = wrong switch
        {
            "verifier" => await TokenAsync(app, code, secret, verifier: NewVerifier()),
            "secret" => await TokenAsync(app, code, blogSecret),
            "redirect" => await TokenAsync(app, code, secret, redirect: "https://shop.sites.test/other"),
            "client" => await TokenAsync(app, code, blogSecret, client: "blog", redirect: "https://shop.sites.test/cb"),
            _ => await TokenAsync(app, NewVerifier(), secret),
        };
        var after = await TokenAsync(app, code, secret);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_grant", await ErrorCodeAsync(response));
        // A wrong try spends the code: a stolen code cannot be guessed at.
        if (wrong is "verifier" or "redirect") Assert.Equal(HttpStatusCode.BadRequest, after.StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-42")]
    public async Task AUTH42_another_grant_type_is_refused()
    {
        await using var app = await StartSitesAsync(postgres);

        var response = await NoJar(app).PostAsync("/oauth/token",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "refresh_token", ["refresh_token"] = "x" }), Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("unsupported_grant_type", await ErrorCodeAsync(response));
    }

    [Fact]
    [Trait("Rule", "AUTH-42")]
    public async Task AUTH42_the_access_token_ends_after_its_time()
    {
        await using var app = await StartSitesAsync(postgres);
        var (_, secret) = await ClientAsync(app);
        var (browser, _) = await app.LoginAsync();
        var tokens = await TokensAsync(app, await CodeAsync(browser, Query()), secret);

        app.Time.Advance(TimeSpan.FromMinutes(10).Add(TimeSpan.FromSeconds(1)));
        var info = await UserInfoAsync(app, tokens.GetProperty("access_token").GetString()!);
        var none = await NoJar(app).GetAsync("/oauth/userinfo", Cancel);

        Assert.Equal(HttpStatusCode.Unauthorized, info.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, none.StatusCode);
        Assert.StartsWith("Bearer", info.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);
    }

    // ---- AUTH-43 what a site may know ----

    [Fact]
    [Trait("Rule", "AUTH-43")]
    public async Task AUTH43_two_sites_get_different_stable_ids_and_never_the_phone()
    {
        await using var app = await StartSitesAsync(postgres);
        var (owner, shopSecret) = await ClientAsync(app);
        (await owner.PostAsJsonAsync("/api/sites", new { name = "blog" }, Cancel)).EnsureSuccessStatusCode();
        var blogSecret = await TurnOnAsync(owner, "blog");
        var (browser, userId) = await app.LoginAsync("Sara", "09121112233");

        var shop = await TokensAsync(app, await CodeAsync(browser, Query()), shopSecret);
        var blog = await TokensAsync(app, await CodeAsync(browser, Query(client: "blog")), blogSecret, client: "blog");
        var shopAgain = await TokensAsync(app, await CodeAsync(browser, Query()), shopSecret);
        var info = await (await UserInfoAsync(app, shop.GetProperty("access_token").GetString()!)).Content.ReadAsStringAsync(Cancel);

        var shopSub = Claims(shop.GetProperty("id_token").GetString()!).GetProperty("sub").GetString();
        var blogSub = Claims(blog.GetProperty("id_token").GetString()!).GetProperty("sub").GetString();
        Assert.NotEqual(shopSub, blogSub);
        Assert.Equal(shopSub, Claims(shopAgain.GetProperty("id_token").GetString()!).GetProperty("sub").GetString());
        Assert.Matches("^[A-Za-z0-9_-]{43}$", shopSub);
        Assert.NotEqual(userId.ToString(System.Globalization.CultureInfo.InvariantCulture), shopSub);
        foreach (var text in new[] { shop.ToString(), blog.ToString(), info, Claims(shop.GetProperty("id_token").GetString()!).ToString() })
        {
            Assert.DoesNotContain("9121112233", text, StringComparison.Ordinal);
            Assert.DoesNotContain("phone", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Rule", "AUTH-43")]
    public async Task AUTH43_email_left_unticked_is_never_given()
    {
        await using var app = await StartSitesAsync(postgres);
        var (_, secret) = await ClientAsync(app);
        var browser = await app.LoginByEmailAsync("sara@example.com");
        var query = Query(scope: "openid profile email");

        var consent = (await JsonAsync(await AuthorizeAsync(browser, query))).GetProperty("consent");
        var tokens = await TokensAsync(app, await CodeAsync(browser, query, email: false), secret);
        var info = await (await UserInfoAsync(app, tokens.GetProperty("access_token").GetString()!)).Content.ReadAsStringAsync(Cancel);

        Assert.True(consent.GetProperty("ask_email").GetBoolean());
        Assert.Equal("sara@example.com", consent.GetProperty("email").GetString());
        Assert.False(Claims(tokens.GetProperty("id_token").GetString()!).TryGetProperty("email", out _));
        Assert.DoesNotContain("sara@example.com", info, StringComparison.Ordinal);
        Assert.DoesNotContain("email", info, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "AUTH-43")]
    public async Task AUTH43_email_ticked_is_given_as_verified()
    {
        await using var app = await StartSitesAsync(postgres);
        var (_, secret) = await ClientAsync(app);
        var browser = await app.LoginByEmailAsync("sara@example.com");

        var tokens = await TokensAsync(app, await CodeAsync(browser, Query(scope: "openid profile email"), email: true), secret);
        var claims = Claims(tokens.GetProperty("id_token").GetString()!);
        var info = await JsonAsync(await UserInfoAsync(app, tokens.GetProperty("access_token").GetString()!));

        Assert.Equal("sara@example.com", claims.GetProperty("email").GetString());
        Assert.True(claims.GetProperty("email_verified").GetBoolean());
        Assert.Equal("sara@example.com", info.GetProperty("email").GetString());
        Assert.True(info.GetProperty("email_verified").GetBoolean());
    }

    [Fact]
    [Trait("Rule", "AUTH-43")]
    public async Task AUTH43_without_profile_there_is_no_name()
    {
        await using var app = await StartSitesAsync(postgres);
        var (_, secret) = await ClientAsync(app);
        var (browser, _) = await app.LoginAsync("Sara");

        var tokens = await TokensAsync(app, await CodeAsync(browser, Query(scope: "openid")), secret);
        var info = await JsonAsync(await UserInfoAsync(app, tokens.GetProperty("access_token").GetString()!));

        Assert.False(Claims(tokens.GetProperty("id_token").GetString()!).TryGetProperty("name", out _));
        Assert.False(info.TryGetProperty("name", out _));
        Assert.True(info.TryGetProperty("sub", out _));
    }

    [Fact]
    [Trait("Rule", "AUTH-43")]
    public async Task AUTH43_login_and_security_lists_the_sites_and_removing_one_asks_again()
    {
        await using var app = await StartSitesAsync(postgres);
        await ClientAsync(app);
        var (browser, _) = await app.LoginAsync();
        await CodeAsync(browser, Query());

        var list = await JsonAsync(await browser.GetAsync("/api/account/allowed-sites", Cancel));
        var removed = await browser.DeleteAsync("/api/account/allowed-sites/shop", Cancel);
        var after = await JsonAsync(await browser.GetAsync("/api/account/allowed-sites", Cancel));
        var again = await JsonAsync(await AuthorizeAsync(browser, Query()));

        var site = Assert.Single(list.GetProperty("sites").EnumerateArray());
        Assert.Equal("shop", site.GetProperty("client_id").GetString());
        Assert.Equal("https://shop.sites.test", site.GetProperty("url").GetString());
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Empty(after.GetProperty("sites").EnumerateArray());
        Assert.True(again.TryGetProperty("consent", out _));
    }

    [Fact]
    [Trait("Rule", "AUTH-43")]
    public async Task AUTH43_authorize_and_token_are_in_the_access_log()
    {
        await using var app = await StartSitesAsync(postgres);
        var (_, secret) = await ClientAsync(app);
        var (browser, userId) = await app.LoginAsync();
        DalanApp.From(browser, "5.6.7.8:41000");
        var code = await CodeAsync(browser, Query());
        var ownerId = await PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT owner_user_id FROM identity.oauth_clients");

        await TokensAsync(app, code, secret);
        var log = await app.AccessLogAsync();

        Assert.Contains($"User {userId} 5.6.7.8 41000", log);
        Assert.Contains($"User {ownerId} 80.1.2.3 44000", log);
    }
}
