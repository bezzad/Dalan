using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dalan.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Identity.Tests;

public class SessionTests(PostgresServer postgres)
{
    private const string Cookie = "__Host-dalan_session";
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private async Task<DalanApp> StartAsync(params (string Key, string Value)[] settings)
    {
        var app = new DalanApp(await postgres.NewDatabaseAsync(), settings.ToDictionary(s => s.Key, s => s.Value));
        await app.ReadyAsync();
        return app;
    }

    /// <summary>Logs in without a cookie jar and returns the Set-Cookie header and the token in it.</summary>
    private static async Task<(string SetCookie, string Token)> LoginRawAsync(DalanApp app, string phone = "09121234567")
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var response = await client.PostAsJsonAsync("/api/dev/login", new { phone, name = "Sara" }, Cancel);
        response.EnsureSuccessStatusCode();
        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith($"{Cookie}=", StringComparison.Ordinal));
        return (setCookie, setCookie.Split(';')[0][(Cookie.Length + 1)..]);
    }

    private static async Task<HttpResponseMessage> MeAsync(DalanApp app, string token)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", $"{Cookie}={token}");
        return await client.GetAsync("/api/me", Cancel);
    }

    private static JsonElement Part(string token, int index) =>
        JsonSerializer.Deserialize<JsonElement>(Base64Url.DecodeFromChars(token.Split('.')[index]));

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Cancel)).GetProperty("error").GetString();

    [Fact]
    [Trait("Rule", "AUTH-03")]
    public async Task AUTH03_login_sets_a_cookie_that_scripts_and_other_sites_cannot_use()
    {
        await using var app = await StartAsync();

        var (setCookie, _) = await LoginRawAsync(app);

        var flags = setCookie.Split(';').Skip(1).Select(flag => flag.Trim().ToLowerInvariant()).ToList();
        Assert.StartsWith($"{Cookie}=", setCookie, StringComparison.Ordinal);
        Assert.Contains("httponly", flags);
        Assert.Contains("secure", flags);
        Assert.Contains("samesite=strict", flags);
        Assert.Contains("path=/", flags);
        Assert.Contains("max-age=900", flags);
        Assert.DoesNotContain(flags, flag => flag.StartsWith("domain", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Rule", "AUTH-03")]
    public async Task AUTH03_token_is_an_es256_jwt_with_the_user_id_and_the_lifetime()
    {
        await using var app = await StartAsync(("Identity:AccessTokenMinutes", "15"));

        var (_, token) = await LoginRawAsync(app);

        var userId = await PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT id FROM identity.users");
        Assert.Equal("ES256", Part(token, 0).GetProperty("alg").GetString());
        Assert.Equal(userId.ToString(System.Globalization.CultureInfo.InvariantCulture), Part(token, 1).GetProperty("sub").GetString());
        Assert.Equal(app.Time.GetUtcNow().AddMinutes(15).ToUnixTimeSeconds(), Part(token, 1).GetProperty("exp").GetInt64());
    }

    [Fact]
    [Trait("Rule", "AUTH-03")]
    public async Task AUTH03_session_ends_after_its_lifetime()
    {
        await using var app = await StartAsync(("Identity:AccessTokenMinutes", "15"));
        var (_, token) = await LoginRawAsync(app);

        app.Time.Advance(TimeSpan.FromMinutes(14));
        Assert.Equal(HttpStatusCode.OK, (await MeAsync(app, token)).StatusCode);
        app.Time.Advance(TimeSpan.FromMinutes(1));
        var expired = await MeAsync(app, token);

        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        Assert.Equal("unauthorized", await ErrorCodeAsync(expired));
    }

    [Fact]
    [Trait("Rule", "AUTH-03")]
    public async Task AUTH03_changed_token_is_refused()
    {
        await using var app = await StartAsync();
        var (_, token) = await LoginRawAsync(app);
        var parts = token.Split('.');
        var otherUser = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(
            Encoding.UTF8.GetString(Base64Url.DecodeFromChars(parts[1])).Replace("\"sub\":\"1\"", "\"sub\":\"2\"", StringComparison.Ordinal)));
        Assert.NotEqual(parts[1], otherUser);

        var response = await MeAsync(app, $"{parts[0]}.{otherUser}.{parts[2]}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthorized", await ErrorCodeAsync(response));
    }

    [Theory]
    [Trait("Rule", "AUTH-03")]
    [InlineData("not-a-token")]
    [InlineData("eyJhbGciOiJub25lIn0.eyJzdWIiOiIxIiwiZXhwIjo0MTAyNDQ0ODAwfQ.")] // "alg": "none"
    public async Task AUTH03_token_that_we_did_not_sign_is_refused(string token)
    {
        await using var app = await StartAsync();
        await LoginRawAsync(app);

        Assert.Equal(HttpStatusCode.Unauthorized, (await MeAsync(app, token)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-04")]
    public async Task AUTH04_me_returns_the_public_profile_without_the_phone()
    {
        await using var app = await StartAsync();
        var (browser, userId) = await app.LoginAsync("سارا", phone: "09121234567");

        var response = await browser.GetAsync("/api/me", Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Cancel);
        var me = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.Equal(userId, me.GetProperty("id").GetInt64());
        Assert.Equal("سارا", me.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, me.GetProperty("username").ValueKind);
        Assert.DoesNotContain("9121234567", body, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "AUTH-04")]
    public async Task AUTH04_me_without_a_session_is_401()
    {
        await using var app = await StartAsync();

        var response = await app.Browser().GetAsync("/api/me", Cancel);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthorized", await ErrorCodeAsync(response));
    }

    [Fact]
    [Trait("Rule", "AUTH-05")]
    public async Task AUTH05_logout_removes_the_session()
    {
        await using var app = await StartAsync();
        var (browser, _) = await app.LoginAsync();

        var logout = await browser.PostAsync("/api/logout", content: null, Cancel);

        Assert.True(logout.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/me", Cancel)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-06")]
    public async Task AUTH06_setting_off_means_no_login_endpoint()
    {
        await using var app = await StartAsync(("Identity:DevLogin", "false"));

        var response = await app.Browser().PostAsJsonAsync("/api/dev/login", new { phone = "09121234567", name = "Sara" }, Cancel);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0L, await PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT count(*) FROM identity.users"));
    }

    [Fact]
    [Trait("Rule", "AUTH-06")]
    public async Task AUTH06_release_settings_have_the_login_off()
    {
        await using var app = await StartAsync();
        var contentRoot = app.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath;

        // appsettings.json is the only settings file in the release (DEL-03).
        var settings = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(Path.Combine(contentRoot, "appsettings.json"), Cancel));

        Assert.False(settings.GetProperty("Identity").GetProperty("DevLogin").GetBoolean());
    }

    [Fact]
    [Trait("Rule", "AUTH-06")]
    public async Task AUTH06_production_with_the_setting_off_has_no_login_endpoint()
    {
        await using var app = new DalanApp(
            await postgres.NewDatabaseAsync(),
            new Dictionary<string, string> { ["Identity:DevLogin"] = "false" },
            web => web.UseEnvironment("Production"));
        await app.ReadyAsync();

        var response = await app.Browser().PostAsJsonAsync("/api/dev/login", new { phone = "09121234567", name = "Sara" }, Cancel);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-06")]
    public async Task AUTH06_setting_on_outside_development_stops_the_server_from_starting()
    {
        await using var app = new DalanApp(
            await postgres.NewDatabaseAsync(),
            new Dictionary<string, string> { ["Identity:DevLogin"] = "true" },
            web => web.UseEnvironment("Production"));

        var error = Assert.Throws<InvalidOperationException>(() => app.CreateClient());

        Assert.Contains("Identity:DevLogin", error.Message, StringComparison.Ordinal);
        Assert.Contains("Development", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "AUTH-06")]
    public async Task AUTH06_new_number_creates_the_user_and_starts_a_session()
    {
        await using var app = await StartAsync();
        var browser = app.Browser();

        var response = await browser.PostAsJsonAsync("/api/dev/login", new { phone = "09121234567", name = "Sara" }, Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("+989121234567|Sara", await PostgresServer.ScalarAsync<string>(
            app.ConnectionString, "SELECT phone || '|' || display_name FROM identity.users"));
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/me", Cancel)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-06")]
    public async Task AUTH06_same_number_again_is_the_same_user()
    {
        await using var app = await StartAsync();

        var (_, first) = await app.LoginAsync(phone: "09121234567");
        var (_, second) = await app.LoginAsync(phone: "+989121234567");

        Assert.Equal(first, second);
        Assert.Equal(1L, await PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT count(*) FROM identity.users"));
    }

    [Theory]
    [Trait("Rule", "AUTH-06")]
    [InlineData("""{"phone":"02112345678","name":"Sara"}""")] // a landline
    [InlineData("""{"phone":"0912123456","name":"Sara"}""")] // too short
    [InlineData("""{"phone":"+19121234567","name":"Sara"}""")] // not Iran
    [InlineData("""{"phone":"09121234567","name":" "}""")]
    [InlineData("""{"phone":"09121234567"}""")]
    [InlineData("""{"phone":"09121234567","name":"a name that is longer than the limit"}""")]
    [InlineData("not json")]
    public async Task AUTH06_bad_phone_or_name_is_400(string body)
    {
        await using var app = await StartAsync(("Identity:MaxNameLength", "20"));
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await app.Browser().PostAsync("/api/dev/login", content, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_request", await ErrorCodeAsync(response));
        Assert.Equal(0L, await PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT count(*) FROM identity.users"));
    }

    [Fact]
    [Trait("Rule", "SEC-05")]
    public async Task SEC05_login_as_a_form_post_is_refused()
    {
        await using var app = await StartAsync();
        using var form = new FormUrlEncodedContent([new("phone", "09121234567"), new("name", "Sara")]);

        var response = await app.Browser().PostAsync("/api/dev/login", form, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<(DalanApp First, DalanApp Second)> TwoServersAsync(string? signingKey)
    {
        var database = await postgres.NewDatabaseAsync();
        var settings = signingKey is null ? null : new Dictionary<string, string> { ["Identity:SigningKey"] = signingKey };
        var first = new DalanApp(database, settings);
        await first.ReadyAsync();
        var second = new DalanApp(database, settings);
        await second.ReadyAsync();
        return (first, second);
    }

    [Fact]
    [Trait("Rule", "AUTH-07")]
    public async Task AUTH07_two_servers_with_the_same_key_accept_each_others_sessions()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (first, second) = await TwoServersAsync(Convert.ToBase64String(key.ExportPkcs8PrivateKey()));
        await using var _ = first;
        await using var __ = second;

        var (_, token) = await LoginRawAsync(first);

        Assert.Equal(HttpStatusCode.OK, (await MeAsync(second, token)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-07")]
    public async Task AUTH07_without_a_key_each_server_has_its_own_temporary_key()
    {
        var (first, second) = await TwoServersAsync(signingKey: null);
        await using var _ = first;
        await using var __ = second;

        var (_, token) = await LoginRawAsync(first);

        Assert.Equal(HttpStatusCode.OK, (await MeAsync(first, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await MeAsync(second, token)).StatusCode);
    }
}
