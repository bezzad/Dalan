using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Testing;
using static Dalan.Identity.Tests.Http;

namespace Dalan.Identity.Tests;

public class AccessKeyTests(PostgresServer postgres)
{
    private const string AdminPhone = "09129990001";

    private Task<DalanApp> StartAsync(params (string Key, string Value)[] settings) =>
        Http.StartAsync(postgres, [.. settings, ("Identity:AdminPhones", AdminPhone)]);

    /// <summary>A logged-in user that the admin approved as a builder.</summary>
    private static async Task<(HttpClient Browser, long UserId, HttpClient Admin)> BuilderAsync(DalanApp app, string name = "Builder")
    {
        var (admin, _) = await app.LoginAsync("Owner", AdminPhone);
        var (browser, userId) = await app.LoginAsync(name);
        (await admin.PostAsJsonAsync($"/api/admin/users/{userId}/builder", new { approve = true }, Cancel)).EnsureSuccessStatusCode();
        return (browser, userId, admin);
    }

    private static async Task<JsonElement> MakeKeyAsync(HttpClient browser, string name = "laptop")
    {
        var response = await browser.PostAsJsonAsync("/api/account/keys", new { name }, Cancel);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await JsonAsync(response);
    }

    /// <summary>A terminal or an agent: no cookies, only the key.</summary>
    private static HttpClient WithKey(DalanApp app, string key)
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return client;
    }

    private static string KeyOf(JsonElement made) => made.GetProperty("key").GetString()!;

    // ---- AUTH-21 make a key ----

    [Fact]
    [Trait("Rule", "AUTH-21")]
    public async Task AUTH21_make_a_key()
    {
        await using var app = await StartAsync();
        var (browser, userId) = await app.LoginAsync();

        var made = await MakeKeyAsync(browser);

        Assert.Matches("^dalan_[A-Za-z0-9_-]{43}$", KeyOf(made));
        Assert.Equal("laptop", made.GetProperty("name").GetString());
        Assert.Equal(app.Time.GetUtcNow().AddDays(90), made.GetProperty("expires_at").GetDateTimeOffset());
        Assert.Equal($"{userId} key.create laptop", await PostgresServer.ScalarAsync<string>(app.ConnectionString,
            "SELECT user_id || ' ' || action || ' ' || coalesce(detail, '') FROM admin.audit_log WHERE action = 'key.create'"));
    }

    [Fact]
    [Trait("Rule", "AUTH-21")]
    public async Task AUTH21_only_the_hash_is_stored()
    {
        await using var app = await StartAsync();
        var (browser, _) = await app.LoginAsync();
        var key = KeyOf(await MakeKeyAsync(browser));

        var row = await PostgresServer.ScalarAsync<string>(app.ConnectionString,
            "SELECT row_to_json(k)::text || encode(key_hash, 'escape') FROM identity.access_keys k");
        var list = await (await browser.GetAsync("/api/account/keys", Cancel)).Content.ReadAsStringAsync(Cancel);

        Assert.DoesNotContain(key[6..], row, StringComparison.Ordinal);
        Assert.Contains("laptop", list, StringComparison.Ordinal);
        Assert.DoesNotContain(key, list, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "AUTH-21")]
    public async Task AUTH21_too_many_keys_gets_409()
    {
        await using var app = await StartAsync(("Identity:MaxAccessKeys", "1"));
        var (browser, _) = await app.LoginAsync();
        await MakeKeyAsync(browser);

        var response = await browser.PostAsJsonAsync("/api/account/keys", new { name = "second" }, Cancel);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("key_limit", await ErrorCodeAsync(response));
    }

    [Theory]
    [Trait("Rule", "AUTH-21")]
    [InlineData("")]
    [InlineData("  ")]
    public async Task AUTH21_no_name_gets_400(string name)
    {
        await using var app = await StartAsync();
        var (browser, _) = await app.LoginAsync();

        var response = await browser.PostAsJsonAsync("/api/account/keys", new { name }, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_request", await ErrorCodeAsync(response));
    }

    // ---- AUTH-23 list and delete ----

    [Fact]
    [Trait("Rule", "AUTH-23")]
    public async Task AUTH23_delete_stops_the_key()
    {
        await using var app = await StartAsync();
        var (browser, _, _) = await BuilderAsync(app);
        var made = await MakeKeyAsync(browser);
        var terminal = WithKey(app, KeyOf(made));
        Assert.Equal(HttpStatusCode.OK, (await terminal.GetAsync("/api/builder/bots", Cancel)).StatusCode);

        var response = await browser.DeleteAsync($"/api/account/keys/{made.GetProperty("id").GetInt64()}", Cancel);
        var list = await JsonAsync(await browser.GetAsync("/api/account/keys", Cancel));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(list.GetProperty("keys").EnumerateArray());
        Assert.Equal(HttpStatusCode.Unauthorized, (await terminal.GetAsync("/api/builder/bots", Cancel)).StatusCode);
        Assert.Equal(1, await PostgresServer.ScalarAsync<long>(app.ConnectionString,
            "SELECT count(*) FROM admin.audit_log WHERE action = 'key.delete'"));
    }

    [Fact]
    [Trait("Rule", "AUTH-23")]
    public async Task AUTH23_another_users_key_gets_404()
    {
        await using var app = await StartAsync();
        var (alice, _, _) = await BuilderAsync(app, "Alice");
        var (bob, _) = await app.LoginAsync("Bob");
        var made = await MakeKeyAsync(alice);

        var response = await bob.DeleteAsync($"/api/account/keys/{made.GetProperty("id").GetInt64()}", Cancel);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("key_not_found", await ErrorCodeAsync(response));
        Assert.Equal(HttpStatusCode.OK, (await WithKey(app, KeyOf(made)).GetAsync("/api/builder/bots", Cancel)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-23")]
    public async Task AUTH23_a_key_cannot_make_list_or_delete_keys()
    {
        await using var app = await StartAsync();
        var (browser, _, _) = await BuilderAsync(app);
        var made = await MakeKeyAsync(browser);
        var terminal = WithKey(app, KeyOf(made));

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await terminal.PostAsJsonAsync("/api/account/keys", new { name = "more" }, Cancel)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await terminal.GetAsync("/api/account/keys", Cancel)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await terminal.DeleteAsync($"/api/account/keys/{made.GetProperty("id").GetInt64()}", Cancel)).StatusCode);
    }

    // ---- AUTH-22 where a key works ----

    [Fact]
    [Trait("Rule", "AUTH-22")]
    public async Task AUTH22_key_works_at_the_builder_calls()
    {
        await using var app = await StartAsync();
        var (browser, _, _) = await BuilderAsync(app);
        var terminal = WithKey(app, KeyOf(await MakeKeyAsync(browser)));

        var created = await terminal.PostAsJsonAsync("/api/builder/bots", new { username = "weather_bot", name = "Weather" }, Cancel);
        var list = await JsonAsync(await terminal.GetAsync("/api/builder/bots", Cancel));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("weather_bot", Assert.Single(list.GetProperty("bots").EnumerateArray()).GetProperty("username").GetString());
    }

    [Theory]
    [Trait("Rule", "AUTH-22")]
    [InlineData("/api/me")]
    [InlineData("/api/bots")]
    [InlineData("/api/settings")]
    [InlineData("/api/admin/settings")]
    public async Task AUTH22_key_does_not_work_elsewhere(string path)
    {
        await using var app = await StartAsync();
        var (admin, _) = await app.LoginAsync("Owner", AdminPhone);
        var terminal = WithKey(app, KeyOf(await MakeKeyAsync(admin)));

        var response = await terminal.GetAsync(path, Cancel);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthorized", await ErrorCodeAsync(response));
    }

    [Fact]
    [Trait("Rule", "AUTH-22")]
    public async Task AUTH22_expired_key_gets_401()
    {
        await using var app = await StartAsync();
        var (browser, _, _) = await BuilderAsync(app);
        var terminal = WithKey(app, KeyOf(await MakeKeyAsync(browser)));

        app.Time.Advance(TimeSpan.FromDays(91));

        Assert.Equal(HttpStatusCode.Unauthorized, (await terminal.GetAsync("/api/builder/bots", Cancel)).StatusCode);
    }

    [Theory]
    [Trait("Rule", "AUTH-22")]
    [InlineData("dalan_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("dalan_")]
    [InlineData("nothing")]
    public async Task AUTH22_unknown_key_gets_401(string key)
    {
        await using var app = await StartAsync();

        var response = await WithKey(app, key).GetAsync("/api/builder/bots", Cancel);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthorized", await ErrorCodeAsync(response));
    }

    [Fact]
    [Trait("Rule", "AUTH-22")]
    public async Task AUTH22_suspended_users_key_stops_at_once()
    {
        await using var app = await StartAsync();
        var (browser, userId, admin) = await BuilderAsync(app);
        var terminal = WithKey(app, KeyOf(await MakeKeyAsync(browser)));

        (await admin.PostAsJsonAsync($"/api/admin/users/{userId}/suspend", new { suspended = true }, Cancel)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await terminal.GetAsync("/api/builder/bots", Cancel)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-22")]
    public async Task AUTH22_session_token_in_the_header_gets_401()
    {
        await using var app = await StartAsync();
        var (sessionToken, _) = await LoginRawAsync(app);
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me", Cancel)).StatusCode);
    }
}
