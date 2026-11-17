using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Testing;

namespace Dalan.Relay.Tests;

public class BuilderTests(PostgresServer postgres)
{
    private const string AdminPhone = "09129990001";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private Task<DalanApp> StartAsync(params (string Key, string Value)[] settings) =>
        RelayApp.StartAsync(postgres, [.. settings, ("Identity:AdminPhones", AdminPhone)]);

    /// <summary>A logged-in user that the admin approved as a builder.</summary>
    private static async Task<(HttpClient Browser, long UserId)> BuilderAsync(DalanApp app, string name = "Builder")
    {
        var (admin, _) = await app.LoginAsync("Owner", AdminPhone);
        var (browser, userId) = await app.LoginAsync(name);
        (await admin.PostAsJsonAsync($"/api/admin/users/{userId}/builder", new { approve = true }, Cancel)).EnsureSuccessStatusCode();
        return (browser, userId);
    }

    private static Task<HttpResponseMessage> CreateAsync(HttpClient browser, string username, string name = "Weather") =>
        browser.PostAsJsonAsync("/api/builder/bots", new { username, name }, Cancel);

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Cancel);

    private static HttpClient Bot(DalanApp app, string token)
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    [Trait("Rule", "REL-36")]
    public async Task REL36_builder_creates_a_bot_and_its_token_works()
    {
        await using var app = await StartAsync();
        var (browser, _) = await BuilderAsync(app);

        var response = await CreateAsync(browser, "@Weather_Bot");
        var bot = await JsonOf(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("weather_bot", bot.GetProperty("username").GetString());
        Assert.Equal("Weather", bot.GetProperty("name").GetString());
        Assert.Empty(await Bot(app, bot.GetProperty("token").GetString()!).UpdatesAsync());
    }

    [Fact]
    [Trait("Rule", "REL-36")]
    public async Task REL36_admin_counts_as_a_builder()
    {
        await using var app = await StartAsync();
        var (admin, _) = await app.LoginAsync("Owner", AdminPhone);

        Assert.Equal(HttpStatusCode.Created, (await CreateAsync(admin, "owner_bot")).StatusCode);
    }

    [Fact]
    [Trait("Rule", "REL-36")]
    public async Task REL36_not_a_builder_gets_403()
    {
        await using var app = await StartAsync();
        var (browser, _) = await app.LoginAsync();
        (await browser.PostAsync("/api/account/builder", null, Cancel)).EnsureSuccessStatusCode();

        var response = await CreateAsync(browser, "weather_bot");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("not_builder", await response.ErrorCodeAsync());
        Assert.Equal(0, await PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT count(*) FROM relay.bots"));
    }

    [Fact]
    [Trait("Rule", "REL-36")]
    public async Task REL36_taken_username_gets_409()
    {
        await using var app = await StartAsync();
        var (browser, _) = await BuilderAsync(app);
        await app.AddBotAsync("weather_bot");

        var response = await CreateAsync(browser, "Weather_Bot");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("username_taken", await response.ErrorCodeAsync());
    }

    [Theory]
    [Trait("Rule", "REL-36")]
    [InlineData("bot", "Name")]
    [InlineData("1weather", "Name")]
    [InlineData("weather-bot", "Name")]
    [InlineData("weather_bot", "")]
    [InlineData("weather_bot", "ABCDEFGHIJK")]
    public async Task REL36_bad_username_or_name_gets_400(string username, string name)
    {
        await using var app = await StartAsync(("Relay:MaxBotNameLength", "10"));
        var (browser, _) = await BuilderAsync(app);

        var response = await CreateAsync(browser, username, name);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_request", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "REL-36")]
    public async Task REL36_too_many_bots_gets_409()
    {
        await using var app = await StartAsync(("Relay:MaxBotsPerBuilder", "1"));
        var (browser, _) = await BuilderAsync(app);
        (await CreateAsync(browser, "first_bot")).EnsureSuccessStatusCode();

        var response = await CreateAsync(browser, "second_bot");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("bot_limit", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "REL-36")]
    public async Task REL36_creation_is_in_the_audit_log_without_the_token()
    {
        await using var app = await StartAsync();
        var (browser, userId) = await BuilderAsync(app);
        var token = (await JsonOf(await CreateAsync(browser, "weather_bot"))).GetProperty("token").GetString()!;

        var row = await PostgresServer.ScalarAsync<string>(app.ConnectionString,
            "SELECT user_id || ' ' || action || ' ' || target || ' ' || coalesce(detail, '') FROM admin.audit_log WHERE action = 'bot.create'");

        Assert.StartsWith($"{userId} bot.create bot ", row, StringComparison.Ordinal);
        Assert.DoesNotContain(token.Split(':')[1], row, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "REL-39")]
    public async Task REL39_list_has_only_my_bots_and_no_token()
    {
        await using var app = await StartAsync();
        var (alice, _) = await BuilderAsync(app, "Alice");
        var (bob, _) = await BuilderAsync(app, "Bob");
        (await CreateAsync(alice, "alice_bot")).EnsureSuccessStatusCode();
        (await CreateAsync(bob, "bob_bot")).EnsureSuccessStatusCode();

        var response = await alice.GetAsync("/api/builder/bots", Cancel);
        var text = await response.Content.ReadAsStringAsync(Cancel);
        var bots = (await JsonOf(response)).GetProperty("bots").EnumerateArray().ToList();

        var bot = Assert.Single(bots);
        Assert.Equal("alice_bot", bot.GetProperty("username").GetString());
        Assert.False(bot.GetProperty("suspended").GetBoolean());
        Assert.DoesNotContain("token", text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "REL-37")]
    public async Task REL37_rotation_stops_the_old_token()
    {
        await using var app = await StartAsync();
        var (browser, _) = await BuilderAsync(app);
        var created = await JsonOf(await CreateAsync(browser, "weather_bot"));
        var oldToken = created.GetProperty("token").GetString()!;

        var rotated = await browser.PostAsync($"/api/builder/bots/{created.GetProperty("id").GetInt64()}/token", null, Cancel);
        var newToken = (await JsonOf(rotated)).GetProperty("token").GetString()!;

        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        Assert.NotEqual(oldToken, newToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Bot(app, oldToken).GetAsync("/v1/updates?timeout=0", Cancel)).StatusCode);
        Assert.Empty(await Bot(app, newToken).UpdatesAsync());
        Assert.Equal(1, await PostgresServer.ScalarAsync<long>(app.ConnectionString,
            "SELECT count(*) FROM admin.audit_log WHERE action = 'bot.token' AND detail IS NULL"));
    }

    [Fact]
    [Trait("Rule", "REL-37")]
    public async Task REL37_another_owners_bot_gets_404()
    {
        await using var app = await StartAsync();
        var (alice, _) = await BuilderAsync(app, "Alice");
        var (bob, _) = await BuilderAsync(app, "Bob");
        var created = await JsonOf(await CreateAsync(alice, "alice_bot"));

        var response = await bob.PostAsync($"/api/builder/bots/{created.GetProperty("id").GetInt64()}/token", null, Cancel);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("bot_not_found", await response.ErrorCodeAsync());
        Assert.Empty(await Bot(app, created.GetProperty("token").GetString()!).UpdatesAsync());
    }
}
