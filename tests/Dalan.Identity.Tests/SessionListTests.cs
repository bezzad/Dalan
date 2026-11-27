using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Testing;
using static Dalan.Identity.Tests.Http;

namespace Dalan.Identity.Tests;

/// <summary>AUTH-35: the list of a user's sessions, and ending them.</summary>
public class SessionListTests(PostgresServer postgres)
{
    private const string Phone = "09121234567";
    private const string AndroidChrome =
        "Mozilla/5.0 (Linux; Android 14; SM-A546E) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Mobile Safari/537.36";
    private const string WindowsFirefox = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:133.0) Gecko/20100101 Firefox/133.0";

    /// <summary>A browser with its own user agent and address that logs the same person in.</summary>
    private static async Task<HttpClient> DeviceAsync(DalanApp app, string userAgent, string endPoint, string phone = Phone)
    {
        var browser = DalanApp.From(app.Browser(), endPoint);
        browser.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        (await browser.PostAsJsonAsync("/api/dev/login", new { phone, name = "Sara" }, Cancel)).EnsureSuccessStatusCode();
        return browser;
    }

    private static async Task<JsonElement[]> SessionsAsync(HttpClient browser)
    {
        var response = await browser.GetAsync("/api/account/sessions", Cancel);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return [.. (await JsonAsync(response)).GetProperty("sessions").EnumerateArray()];
    }

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient browser) => browser.PostAsync("/api/auth/refresh", null, Cancel);

    private static Task<long> CountAsync(DalanApp app, string sql) => PostgresServer.ScalarAsync<long>(app.ConnectionString, sql);

    [Fact]
    [Trait("Rule", "AUTH-35")]
    public async Task AUTH35_list_has_both_devices_and_only_the_callers_is_current()
    {
        await using var app = await StartAsync(postgres);
        await DeviceAsync(app, AndroidChrome, "5.6.7.8:41000");
        app.Time.Advance(TimeSpan.FromMinutes(1));
        var laptop = await DeviceAsync(app, WindowsFirefox, "80.244.7.9:42000");

        var sessions = await SessionsAsync(laptop);

        Assert.Equal(2, sessions.Length);
        var mine = Assert.Single(sessions, s => s.GetProperty("current").GetBoolean());
        var phone = Assert.Single(sessions, s => !s.GetProperty("current").GetBoolean());
        Assert.Equal("Firefox", mine.GetProperty("browser").GetString());
        Assert.Equal("Windows", mine.GetProperty("system").GetString());
        Assert.Equal("80.244.7.9", mine.GetProperty("address").GetString());
        Assert.Equal("Chrome", phone.GetProperty("browser").GetString());
        Assert.Equal("Android", phone.GetProperty("system").GetString());
        Assert.Equal("5.6.7.8", phone.GetProperty("address").GetString());
        Assert.Equal(JsonValueKind.Number, phone.GetProperty("id").ValueKind);
        Assert.Equal(app.Time.GetUtcNow().AddMinutes(-1), phone.GetProperty("created_at").GetDateTimeOffset());
        Assert.Equal(app.Time.GetUtcNow().AddMinutes(-1), phone.GetProperty("last_used_at").GetDateTimeOffset());
    }

    [Fact]
    [Trait("Rule", "AUTH-35")]
    public async Task AUTH35_last_use_and_address_follow_the_refresh()
    {
        await using var app = await StartAsync(postgres);
        var laptop = await DeviceAsync(app, WindowsFirefox, "80.244.7.9:42000");
        var created = app.Time.GetUtcNow();

        app.Time.Advance(TimeSpan.FromMinutes(20));
        laptop.DefaultRequestHeaders.Remove("Test-Socket");
        DalanApp.From(laptop, "80.244.7.10:42001");
        Assert.Equal(HttpStatusCode.NoContent, (await RefreshAsync(laptop)).StatusCode);

        var session = Assert.Single(await SessionsAsync(laptop));
        Assert.Equal(created, session.GetProperty("created_at").GetDateTimeOffset());
        Assert.Equal(app.Time.GetUtcNow(), session.GetProperty("last_used_at").GetDateTimeOffset());
        Assert.Equal("80.244.7.10", session.GetProperty("address").GetString());
    }

    [Theory]
    [Trait("Rule", "AUTH-35")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1",
        "Safari", "iOS")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36 Edg/131.0.0.0",
        "Edge", "macOS")]
    [InlineData("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36 OPR/115.0.0.0",
        "Opera", "Linux")]
    [InlineData("curl/8.5.0", "", "")]
    public async Task AUTH35_browser_and_system_come_from_the_user_agent(string userAgent, string browserName, string system)
    {
        await using var app = await StartAsync(postgres);
        var device = await DeviceAsync(app, userAgent, "5.6.7.8:41000");

        var session = Assert.Single(await SessionsAsync(device));

        Assert.Equal(browserName, session.GetProperty("browser").GetString());
        Assert.Equal(system, session.GetProperty("system").GetString());
    }

    [Fact]
    [Trait("Rule", "AUTH-35")]
    public async Task AUTH35_log_out_the_others()
    {
        await using var app = await StartAsync(postgres);
        var phone = await DeviceAsync(app, AndroidChrome, "5.6.7.8:41000");
        var laptop = await DeviceAsync(app, WindowsFirefox, "80.244.7.9:42000");

        var response = await laptop.DeleteAsync("/api/account/sessions", Cancel);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(phone)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await RefreshAsync(laptop)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await laptop.GetAsync("/api/me", Cancel)).StatusCode);
        Assert.True(Assert.Single(await SessionsAsync(laptop)).GetProperty("current").GetBoolean());
        Assert.Equal(1, await CountAsync(app, "SELECT count(*) FROM admin.audit_log WHERE action = 'session.end_others' AND detail = '1'"));
    }

    [Fact]
    [Trait("Rule", "AUTH-35")]
    public async Task AUTH35_end_one_session()
    {
        await using var app = await StartAsync(postgres);
        var phone = await DeviceAsync(app, AndroidChrome, "5.6.7.8:41000");
        var laptop = await DeviceAsync(app, WindowsFirefox, "80.244.7.9:42000");
        var phoneId = Assert.Single(await SessionsAsync(laptop), s => !s.GetProperty("current").GetBoolean()).GetProperty("id").GetInt64();

        var response = await laptop.DeleteAsync($"/api/account/sessions/{phoneId}", Cancel);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(phone)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await RefreshAsync(laptop)).StatusCode);
        Assert.Equal(1, await CountAsync(app, $"SELECT count(*) FROM admin.audit_log WHERE action = 'session.end' AND target = 'session {phoneId}'"));
        Assert.Contains(await app.AccessLogAsync(), row => row.EndsWith(" 80.244.7.9 42000", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Rule", "AUTH-35")]
    public async Task AUTH35_another_users_session_gets_404()
    {
        await using var app = await StartAsync(postgres);
        var alice = await DeviceAsync(app, AndroidChrome, "5.6.7.8:41000", "09121110001");
        var bob = await DeviceAsync(app, WindowsFirefox, "80.244.7.9:42000", "09121110002");
        var aliceId = Assert.Single(await SessionsAsync(alice)).GetProperty("id").GetInt64();

        var response = await bob.DeleteAsync($"/api/account/sessions/{aliceId}", Cancel);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("session_not_found", await ErrorCodeAsync(response));
        Assert.Equal(HttpStatusCode.NoContent, (await RefreshAsync(alice)).StatusCode);
        Assert.Single(await SessionsAsync(bob));
    }

    [Fact]
    [Trait("Rule", "AUTH-35")]
    public async Task AUTH35_ended_and_expired_sessions_are_not_listed_and_no_session_gets_401()
    {
        await using var app = await StartAsync(postgres, ("Identity:RefreshTokenDays", "1"));
        await DeviceAsync(app, AndroidChrome, "5.6.7.8:41000");
        app.Time.Advance(TimeSpan.FromDays(2));
        var laptop = await DeviceAsync(app, WindowsFirefox, "80.244.7.9:42000");

        Assert.True(Assert.Single(await SessionsAsync(laptop)).GetProperty("current").GetBoolean());
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Browser().GetAsync("/api/account/sessions", Cancel)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Browser().DeleteAsync("/api/account/sessions", Cancel)).StatusCode);
    }
}
