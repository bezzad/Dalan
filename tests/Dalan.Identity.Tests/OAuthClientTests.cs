using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Testing;
using static Dalan.Identity.Tests.Http;
using static Dalan.Identity.Tests.OAuth;

namespace Dalan.Identity.Tests;

/// <summary>AUTH-40: a site's login client: on, secret, return address rule, off.</summary>
public class OAuthClientTests(PostgresServer postgres)
{
    [Fact]
    [Trait("Rule", "AUTH-40")]
    public async Task AUTH40_turn_on_gives_the_client_id_and_the_secret_once()
    {
        await using var app = await StartSitesAsync(postgres);
        var (owner, userId) = await OwnerAsync(app, "shop");

        var response = await owner.PostAsJsonAsync("/api/oauth/clients", new { site = "shop" }, Cancel);
        var again = await owner.PostAsJsonAsync("/api/oauth/clients", new { site = "shop" }, Cancel);
        var list = await JsonAsync(await owner.GetAsync("/api/oauth/clients", Cancel));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var made = await JsonAsync(response);
        Assert.Equal("shop", made.GetProperty("client_id").GetString());
        var secret = made.GetProperty("secret").GetString()!;
        Assert.Matches("^[A-Za-z0-9_-]{43}$", secret);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.False((await JsonAsync(again)).TryGetProperty("secret", out _));
        var client = Assert.Single(list.GetProperty("clients").EnumerateArray());
        Assert.Equal("shop", client.GetProperty("client_id").GetString());
        Assert.DoesNotContain(secret, list.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, await PostgresServer.ScalarAsync<long>(app.ConnectionString,
            $"SELECT count(*) FROM identity.oauth_clients WHERE secret_hash = sha256('{secret}'::bytea)"));
        Assert.Equal($"{userId} oauth.on site shop", await PostgresServer.ScalarAsync<string>(app.ConnectionString,
            "SELECT user_id || ' ' || action || ' ' || target FROM admin.audit_log WHERE action = 'oauth.on'"));
    }

    [Fact]
    [Trait("Rule", "AUTH-40")]
    public async Task AUTH40_only_the_owner_of_the_site_may_turn_it_on()
    {
        await using var app = await StartSitesAsync(postgres);
        await OwnerAsync(app, "shop");
        var (other, _) = await app.LoginAsync("Other");

        var response = await other.PostAsJsonAsync("/api/oauth/clients", new { site = "shop" }, Cancel);
        var none = await other.PostAsJsonAsync("/api/oauth/clients", new { site = "nosuch" }, Cancel);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("site_not_found", await ErrorCodeAsync(response));
        Assert.Equal(HttpStatusCode.NotFound, none.StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-40")]
    public async Task AUTH40_new_secret_replaces_the_old_one()
    {
        await using var app = await StartSitesAsync(postgres);
        var (owner, old) = await ClientAsync(app);
        var (browser, _) = await app.LoginAsync();

        var response = await owner.PostAsync("/api/oauth/clients/shop/secret", null, Cancel);
        var secret = (await JsonAsync(response)).GetProperty("secret").GetString()!;
        var withOld = await TokenAsync(app, await CodeAsync(browser, Query()), old);
        var withNew = await TokenAsync(app, await CodeAsync(browser, Query()), secret);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(old, secret);
        Assert.Equal(HttpStatusCode.BadRequest, withOld.StatusCode);
        Assert.Equal("invalid_grant", await ErrorCodeAsync(withOld));
        Assert.Equal(HttpStatusCode.OK, withNew.StatusCode);
        Assert.Equal(1, await PostgresServer.ScalarAsync<long>(app.ConnectionString,
            "SELECT count(*) FROM admin.audit_log WHERE action = 'oauth.secret' AND target = 'site shop'"));
    }

    [Theory]
    [Trait("Rule", "AUTH-40")]
    [InlineData("https://other.sites.test/cb")]
    [InlineData("https://a.shop.sites.test/cb")]
    [InlineData("https://shop.sites.test.evil.com/cb")]
    [InlineData("https://shop.sites.test@evil.com/cb")]
    [InlineData("https://user@shop.sites.test/cb")]
    [InlineData("https://shop.sites.test%2F@evil.com/cb")]
    [InlineData("https://shop.sites.test%2fcb")]
    [InlineData("https://shop.sites.test\\@evil.com/cb")]
    [InlineData("https://shop.sites.test:8443/cb")]
    [InlineData("https://shop.sites.test:443/cb")]
    [InlineData("http://shop.sites.test/cb")]
    [InlineData("HTTPS://SHOP.sites.test/cb")]
    [InlineData("https://shop.sites.test")]
    [InlineData("https://shop.sites.test/cb#part")]
    [InlineData("https://shop.sites.test/c b")]
    [InlineData("//shop.sites.test/cb")]
    [InlineData("/cb")]
    [InlineData("javascript:alert(1)//https://shop.sites.test/")]
    [InlineData("https://evil.com/?r=https://shop.sites.test/cb")]
    [InlineData("http://localhost.evil.com/cb")]
    [InlineData("http://localhost@evil.com/cb")]
    [InlineData("http://localhost:99999/cb")]
    [InlineData("https://localhost:3000/cb")]
    [InlineData("http://127.0.0.1:3000/cb")]
    [InlineData("https://shop.sites.test/.dalan/callback")]
    [InlineData("")]
    public async Task AUTH40_a_foreign_return_address_is_refused_on_dalans_page(string redirect)
    {
        await using var app = await StartSitesAsync(postgres);
        await ClientAsync(app);
        var (browser, _) = await app.LoginAsync();

        var response = await AuthorizeAsync(browser, Query(redirect: redirect.Length == 0 ? "x" : redirect));
        var body = await JsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_redirect_uri", body.GetProperty("error").GetString());
        Assert.False(body.TryGetProperty("redirect", out _));
        Assert.Equal(0, await PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT count(*) FROM identity.oauth_codes"));
    }

    [Theory]
    [Trait("Rule", "AUTH-40")]
    [InlineData("https://shop.sites.test/cb")]
    [InlineData("https://shop.sites.test/")]
    [InlineData("https://shop.sites.test/auth/callback?from=dalan")]
    [InlineData("http://localhost:3000/cb")]
    [InlineData("http://localhost/cb")]
    public async Task AUTH40_the_sites_own_host_or_localhost_is_allowed(string redirect)
    {
        await using var app = await StartSitesAsync(postgres);
        await ClientAsync(app);
        var (browser, _) = await app.LoginAsync();

        var response = await AuthorizeAsync(browser, Query(redirect: redirect));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await JsonAsync(response)).TryGetProperty("consent", out _));
    }

    [Fact]
    [Trait("Rule", "AUTH-40")]
    public async Task AUTH40_unknown_client_is_refused_on_dalans_page_even_before_login()
    {
        await using var app = await StartSitesAsync(postgres);
        await OwnerAsync(app, "shop");

        var response = await AuthorizeAsync(app.Browser(), Query());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_client", await ErrorCodeAsync(response));
    }

    [Fact]
    [Trait("Rule", "AUTH-40")]
    public async Task AUTH40_turn_off_ends_the_client()
    {
        await using var app = await StartSitesAsync(postgres);
        var (owner, secret) = await ClientAsync(app);
        var (browser, _) = await app.LoginAsync();
        var code = await CodeAsync(browser, Query());

        var off = await owner.DeleteAsync("/api/oauth/clients/shop", Cancel);
        var authorize = await AuthorizeAsync(browser, Query());
        var token = await TokenAsync(app, code, secret);

        Assert.Equal(HttpStatusCode.NoContent, off.StatusCode);
        Assert.Equal("invalid_client", await ErrorCodeAsync(authorize));
        Assert.Equal(HttpStatusCode.BadRequest, token.StatusCode);
        Assert.Empty((await JsonAsync(await owner.GetAsync("/api/oauth/clients", Cancel))).GetProperty("clients").EnumerateArray());
        Assert.Equal(1, await PostgresServer.ScalarAsync<long>(app.ConnectionString,
            "SELECT count(*) FROM admin.audit_log WHERE action = 'oauth.off' AND target = 'site shop'"));
    }

    [Fact]
    [Trait("Rule", "AUTH-40")]
    public async Task AUTH40_deleting_the_site_ends_the_client()
    {
        await using var app = await StartSitesAsync(postgres);
        var (owner, secret) = await ClientAsync(app);
        var (browser, _) = await app.LoginAsync();
        var code = await CodeAsync(browser, Query());
        var siteId = (await owner.GetFromJsonAsync<JsonElement>("/api/sites", Cancel)).GetProperty("sites")[0].GetProperty("id").GetInt64();

        (await owner.DeleteAsync($"/api/sites/{siteId}", Cancel)).EnsureSuccessStatusCode();
        var authorize = await AuthorizeAsync(browser, Query());
        var token = await TokenAsync(app, code, secret);

        Assert.Equal("invalid_client", await ErrorCodeAsync(authorize));
        Assert.Equal(HttpStatusCode.BadRequest, token.StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-40")]
    public async Task AUTH40_a_new_owner_of_the_name_starts_with_no_client_and_no_consents()
    {
        await using var app = await StartSitesAsync(postgres);
        var (owner, _) = await ClientAsync(app);
        var (browser, _) = await app.LoginAsync();
        await CodeAsync(browser, Query());
        var siteId = (await owner.GetFromJsonAsync<JsonElement>("/api/sites", Cancel)).GetProperty("sites")[0].GetProperty("id").GetInt64();
        (await owner.DeleteAsync($"/api/sites/{siteId}", Cancel)).EnsureSuccessStatusCode();

        var (next, _) = await OwnerAsync(app, "shop");
        await TurnOnAsync(next, "shop");
        var again = await JsonAsync(await AuthorizeAsync(browser, Query()));

        Assert.True(again.TryGetProperty("consent", out _), "a consent given to the old owner must not count");
    }
}
