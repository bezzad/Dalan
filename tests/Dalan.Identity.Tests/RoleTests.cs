using System.Net;
using System.Net.Http.Json;
using Dalan.Testing;
using static Dalan.Identity.Tests.Http;

namespace Dalan.Identity.Tests;

public class RoleTests(PostgresServer postgres)
{
    private const string AdminPhone = "09129990001";

    private Task<DalanApp> StartAsync() => Http.StartAsync(postgres, ("Identity:AdminPhones", $"0912 999 9999, {AdminPhone}"));

    private static Task<HttpResponseMessage> SuspendAsync(HttpClient admin, long userId, bool suspended) =>
        admin.PostAsJsonAsync($"/api/admin/users/{userId}/suspend", new { suspended }, Cancel);

    private static async Task<string?> BuilderAsync(HttpClient browser) =>
        (await JsonAsync(await browser.GetAsync("/api/me", Cancel))).GetProperty("builder").GetString();

    // ---- AUTH-18 admins ----

    [Fact]
    [Trait("Rule", "AUTH-18")]
    public async Task AUTH18_user_with_admin_phone_is_admin()
    {
        await using var app = await StartAsync();
        var (browser, _) = await app.LoginAsync("Owner", "+989129990001");

        Assert.True((await JsonAsync(await browser.GetAsync("/api/me", Cancel))).GetProperty("admin").GetBoolean());
    }

    [Fact]
    [Trait("Rule", "AUTH-18")]
    public async Task AUTH18_other_user_is_not_admin()
    {
        await using var app = await StartAsync();
        var (browser, _) = await app.LoginAsync("Sara", "09121234567");

        Assert.False((await JsonAsync(await browser.GetAsync("/api/me", Cancel))).GetProperty("admin").GetBoolean());
    }

    // ---- AUTH-19 builder request ----

    [Fact]
    [Trait("Rule", "AUTH-19")]
    public async Task AUTH19_ask_sets_requested()
    {
        await using var app = await StartAsync();
        var (browser, _) = await app.LoginAsync();

        var profile = await JsonAsync(await browser.PostAsync("/api/account/builder", null, Cancel));

        Assert.Equal("requested", profile.GetProperty("builder").GetString());
        Assert.Equal("requested", await BuilderAsync(browser));
    }

    [Fact]
    [Trait("Rule", "AUTH-19")]
    public async Task AUTH19_approved_user_can_create_a_bot_at_once()
    {
        await using var app = await StartAsync();
        var (admin, _) = await app.LoginAsync("Owner", AdminPhone);
        var (browser, userId) = await app.LoginAsync();
        (await browser.PostAsync("/api/account/builder", null, Cancel)).EnsureSuccessStatusCode();

        (await admin.PostAsJsonAsync($"/api/admin/users/{userId}/builder", new { approve = true }, Cancel)).EnsureSuccessStatusCode();
        var created = await browser.PostAsJsonAsync("/api/builder/bots", new { username = "sara_bot", name = "Sara's bot" }, Cancel);

        Assert.Equal("approved", await BuilderAsync(browser));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-19")]
    public async Task AUTH19_approved_stays_approved_when_asking_again()
    {
        await using var app = await StartAsync();
        var (admin, _) = await app.LoginAsync("Owner", AdminPhone);
        var (browser, userId) = await app.LoginAsync();
        (await admin.PostAsJsonAsync($"/api/admin/users/{userId}/builder", new { approve = true }, Cancel)).EnsureSuccessStatusCode();

        (await browser.PostAsync("/api/account/builder", null, Cancel)).EnsureSuccessStatusCode();

        Assert.Equal("approved", await BuilderAsync(browser));
    }

    [Fact]
    [Trait("Rule", "AUTH-19")]
    public async Task AUTH19_refused_is_kept_and_may_ask_again()
    {
        await using var app = await StartAsync();
        var (admin, _) = await app.LoginAsync("Owner", AdminPhone);
        var (browser, userId) = await app.LoginAsync();
        (await browser.PostAsync("/api/account/builder", null, Cancel)).EnsureSuccessStatusCode();

        (await admin.PostAsJsonAsync($"/api/admin/users/{userId}/builder", new { approve = false }, Cancel)).EnsureSuccessStatusCode();
        var afterRefusal = await BuilderAsync(browser);
        (await browser.PostAsync("/api/account/builder", null, Cancel)).EnsureSuccessStatusCode();

        Assert.Equal("refused", afterRefusal);
        Assert.Equal("requested", await BuilderAsync(browser));
    }

    [Fact]
    [Trait("Rule", "AUTH-19")]
    public async Task AUTH19_with_auto_builder_asking_approves_at_once()
    {
        await using var app = await Http.StartAsync(postgres, ("Identity:AutoBuilder", "true"));
        var (browser, _) = await app.LoginAsync();
        // The development login already approves with the setting on; start from "none" to test the button.
        await PostgresServer.ExecuteAsync(app.ConnectionString, "UPDATE identity.users SET builder = 0");

        (await browser.PostAsync("/api/account/builder", null, Cancel)).EnsureSuccessStatusCode();

        Assert.Equal("approved", await BuilderAsync(browser));
    }

    [Fact]
    [Trait("Rule", "AUTH-19")]
    public async Task AUTH19_a_refused_users_request_waits_for_the_admin_even_with_auto_builder()
    {
        await using var app = await Http.StartAsync(postgres, ("Identity:AutoBuilder", "true"), ("Identity:AdminPhones", AdminPhone));
        var (admin, _) = await app.LoginAsync("Owner", AdminPhone);
        var (browser, userId) = await app.LoginAsync();

        (await admin.PostAsJsonAsync($"/api/admin/users/{userId}/builder", new { approve = false }, Cancel)).EnsureSuccessStatusCode();
        var afterRefusal = await BuilderAsync(browser);
        (await browser.PostAsync("/api/account/builder", null, Cancel)).EnsureSuccessStatusCode();

        Assert.Equal("refused", afterRefusal);
        Assert.Equal("requested", await BuilderAsync(browser));
    }

    // ---- AUTH-20 suspended user ----

    [Fact]
    [Trait("Rule", "AUTH-20")]
    public async Task AUTH20_session_and_refresh_stop_at_once()
    {
        await using var app = await StartAsync();
        var (admin, _) = await app.LoginAsync("Owner", AdminPhone);
        var (session, refresh) = await LoginRawAsync(app, "09121234567");
        var userId = (await JsonAsync(await SendAsync(app, HttpMethod.Get, "/api/me", session))).GetProperty("id").GetInt64();

        (await SuspendAsync(admin, userId, true)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(app, HttpMethod.Get, "/api/me", session)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(app, HttpMethod.Get, "/api/bots", session)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(app, refresh)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-20")]
    public async Task AUTH20_no_sms_for_a_suspended_user()
    {
        await using var app = await StartAsync();
        var (admin, _) = await app.LoginAsync("Owner", AdminPhone);
        var (_, userId) = await app.LoginAsync("Sara", "09121234567");
        (await SuspendAsync(admin, userId, true)).EnsureSuccessStatusCode();

        var response = await DalanApp.AskCodeAsync(app.Browser(), "09121234567");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("suspended", await ErrorCodeAsync(response));
        Assert.Empty(app.Sms.Sent);
    }

    [Fact]
    [Trait("Rule", "AUTH-20")]
    public async Task AUTH20_password_login_is_refused_and_works_again_after_unsuspend()
    {
        await using var app = await StartAsync();
        var (admin, _) = await app.LoginAsync("Owner", AdminPhone);
        var (browser, userId) = await app.LoginAsync("Sara", "09121234567");
        (await browser.PostAsJsonAsync("/api/account/password", new { password = "a good password" }, Cancel)).EnsureSuccessStatusCode();
        (await SuspendAsync(admin, userId, true)).EnsureSuccessStatusCode();

        var refused = await app.Browser().PostAsJsonAsync("/api/auth/password", new { login = "09121234567", password = "a good password" }, Cancel);
        (await SuspendAsync(admin, userId, false)).EnsureSuccessStatusCode();
        var allowed = await app.Browser().PostAsJsonAsync("/api/auth/password", new { login = "09121234567", password = "a good password" }, Cancel);

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("suspended", await ErrorCodeAsync(refused));
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-20")]
    public async Task AUTH20_suspension_holds_after_a_restart()
    {
        var database = await postgres.NewDatabaseAsync();
        var settings = new Dictionary<string, string> { ["Identity:AdminPhones"] = AdminPhone };
        string session;
        await using (var first = new DalanApp(database, settings))
        {
            await first.ReadyAsync();
            var (admin, _) = await first.LoginAsync("Owner", AdminPhone);
            (session, _) = await LoginRawAsync(first, "09121234567");
            var userId = (await JsonAsync(await SendAsync(first, HttpMethod.Get, "/api/me", session))).GetProperty("id").GetInt64();
            (await SuspendAsync(admin, userId, true)).EnsureSuccessStatusCode();
        }

        await using var second = new DalanApp(database, settings);
        await second.ReadyAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(second, HttpMethod.Get, "/api/me", session)).StatusCode);
    }
}
