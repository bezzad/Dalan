using System.Net;
using System.Net.Http.Json;
using Dalan.Testing;
using static Dalan.Identity.Tests.Http;

namespace Dalan.Identity.Tests;

public class PasswordTests(PostgresServer postgres)
{
    private const string Phone = "09121234567";
    private const string Good = "correct horse";

    private static Task<HttpResponseMessage> SetPasswordAsync(HttpClient browser, string password, string? current = null) =>
        browser.PostAsJsonAsync("/api/account/password", new { password, current_password = current }, Cancel);

    private static Task<HttpResponseMessage> LoginAsync(DalanApp app, string login, string password) =>
        app.Browser().PostAsJsonAsync("/api/auth/password", new { login, password }, Cancel);

    /// <summary>A user with a password, and a browser that logged in with that password.</summary>
    private static async Task<HttpClient> UserLoggedInByPasswordAsync(DalanApp app)
    {
        var (first, _) = await app.LoginAsync(phone: Phone);
        (await SetPasswordAsync(first, Good)).EnsureSuccessStatusCode();
        var browser = app.Browser();
        (await browser.PostAsJsonAsync("/api/auth/password", new { login = Phone, password = Good }, Cancel)).EnsureSuccessStatusCode();
        return browser;
    }

    // ---- AUTH-13 set a password ----

    [Fact]
    [Trait("Rule", "AUTH-13")]
    public async Task AUTH13_first_password_is_stored_as_argon2id()
    {
        await using var app = await StartAsync(postgres, ("Identity:PasswordMemoryKb", "64"), ("Identity:PasswordIterations", "1"));
        var (browser, _) = await app.LoginAsync();

        var response = await SetPasswordAsync(browser, "12345678");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var stored = await PostgresServer.ScalarAsync<string>(app.ConnectionString, "SELECT password_hash FROM identity.users");
        Assert.StartsWith("$argon2id$v=19$m=64,t=1,p=1$", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("12345678", stored, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "AUTH-13")]
    public async Task AUTH13_same_password_twice_gives_two_different_hashes()
    {
        await using var app = await StartAsync(postgres);
        var (first, _) = await app.LoginAsync();
        var (second, _) = await app.LoginAsync();
        await SetPasswordAsync(first, Good);
        await SetPasswordAsync(second, Good);

        Assert.Equal(2L, await PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT count(DISTINCT password_hash) FROM identity.users"));
    }

    [Theory]
    [Trait("Rule", "AUTH-13")]
    [InlineData(7)]
    [InlineData(129)]
    public async Task AUTH13_too_short_or_too_long_is_400(int length)
    {
        await using var app = await StartAsync(postgres, ("Identity:PasswordMinLength", "8"), ("Identity:PasswordMaxLength", "128"));
        var (browser, _) = await app.LoginAsync();

        var response = await SetPasswordAsync(browser, new string('x', length));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("weak_password", await ErrorCodeAsync(response));
    }

    [Theory]
    [Trait("Rule", "AUTH-13")]
    [InlineData(null)]
    [InlineData("not the current one")]
    public async Task AUTH13_change_needs_the_current_password(string? current)
    {
        await using var app = await StartAsync(postgres);
        var browser = await UserLoggedInByPasswordAsync(app);

        var response = await SetPasswordAsync(browser, "a new password", current);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("wrong_password", await ErrorCodeAsync(response));
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(app, Phone, Good)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-13")]
    public async Task AUTH13_change_with_the_current_password()
    {
        await using var app = await StartAsync(postgres);
        var browser = await UserLoggedInByPasswordAsync(app);

        var response = await SetPasswordAsync(browser, "a new password", Good);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(app, Phone, Good)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(app, Phone, "a new password")).StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-13")]
    public async Task AUTH13_forgot_the_password_login_by_sms_sets_a_new_one()
    {
        await using var app = await StartAsync(postgres);
        await UserLoggedInByPasswordAsync(app);
        var bySms = await app.LoginBySmsAsync(Phone);

        var response = await SetPasswordAsync(bySms, "a new password");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(app, Phone, "a new password")).StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-13")]
    public async Task AUTH13_changed_password_logs_out_the_other_devices_only()
    {
        await using var app = await StartAsync(postgres);
        var (phone, _) = await app.LoginAsync(phone: Phone);
        var (laptop, _) = await app.LoginAsync(phone: Phone);

        await SetPasswordAsync(phone, Good);

        Assert.Equal(HttpStatusCode.NoContent, (await phone.PostAsync("/api/auth/refresh", null, Cancel)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await laptop.PostAsync("/api/auth/refresh", null, Cancel)).StatusCode);
    }

    [Theory]
    [Trait("Rule", "AUTH-13")]
    [Trait("Rule", "AUTH-15")]
    [InlineData("/api/account/password")]
    [InlineData("/api/account/username")]
    public async Task AUTH13_AUTH15_account_calls_need_a_session(string path)
    {
        await using var app = await StartAsync(postgres);

        var response = await app.Browser().PostAsJsonAsync(path, new { password = Good, username = "sara_1" }, Cancel);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthorized", await ErrorCodeAsync(response));
    }

    // ---- AUTH-14 log in with a password ----

    [Fact]
    [Trait("Rule", "AUTH-14")]
    public async Task AUTH14_login_by_phone()
    {
        await using var app = await StartAsync(postgres);
        var (first, userId) = await app.LoginAsync(phone: Phone);
        await SetPasswordAsync(first, Good);

        var response = await LoginAsync(app, Phone, Good);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(userId, (await JsonAsync(response)).GetProperty("id").GetInt64());
        Assert.NotNull(SetCookie(response, SessionCookie));
        Assert.NotNull(SetCookie(response, RefreshCookie));
    }

    [Fact]
    [Trait("Rule", "AUTH-14")]
    public async Task AUTH14_login_by_username_in_any_letter_case()
    {
        await using var app = await StartAsync(postgres);
        var (first, _) = await app.LoginAsync();
        await SetPasswordAsync(first, Good);
        await first.PostAsJsonAsync("/api/account/username", new { username = "Sara_1" }, Cancel);

        var response = await LoginAsync(app, "sara_1", Good);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-14")]
    public async Task AUTH14_wrong_password_is_401_and_sets_no_cookie()
    {
        await using var app = await StartAsync(postgres);
        var (first, _) = await app.LoginAsync(phone: Phone);
        await SetPasswordAsync(first, Good);

        var response = await LoginAsync(app, Phone, "wrong horse");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("wrong_login", await ErrorCodeAsync(response));
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    [Trait("Rule", "AUTH-14")]
    public async Task AUTH14_unknown_login_and_user_without_a_password_get_the_same_answer()
    {
        await using var app = await StartAsync(postgres);
        await app.LoginAsync(phone: Phone);

        var noPassword = await LoginAsync(app, Phone, Good);
        var unknown = await LoginAsync(app, "nobody_here", Good);

        Assert.Equal(HttpStatusCode.Unauthorized, noPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(await noPassword.Content.ReadAsStringAsync(Cancel), await unknown.Content.ReadAsStringAsync(Cancel));
    }

    [Fact]
    [Trait("Rule", "AUTH-14")]
    public async Task AUTH14_missing_fields_are_400()
    {
        await using var app = await StartAsync(postgres);

        var response = await app.Browser().PostAsJsonAsync("/api/auth/password", new { login = Phone }, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_request", await ErrorCodeAsync(response));
    }

    // ---- SEC-09 password tries ----

    [Fact]
    [Trait("Rule", "SEC-09")]
    public async Task SEC09_over_the_limit_even_the_right_password_is_429()
    {
        await using var app = await StartAsync(postgres, ("Identity:PasswordTries", "2"));
        var (first, _) = await app.LoginAsync();
        await SetPasswordAsync(first, Good);
        await first.PostAsJsonAsync("/api/account/username", new { username = "Sara_1" }, Cancel);
        await LoginAsync(app, "sara_1", "wrong one");
        await LoginAsync(app, "SARA_1", "wrong two");

        var response = await LoginAsync(app, "Sara_1", Good);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("rate_limited", await ErrorCodeAsync(response));
        Assert.True(int.Parse(Assert.Single(response.Headers.GetValues("Retry-After")), System.Globalization.CultureInfo.InvariantCulture) > 0);
    }

    [Fact]
    [Trait("Rule", "SEC-09")]
    public async Task SEC09_one_login_does_not_use_up_another_logins_tries()
    {
        await using var app = await StartAsync(postgres, ("Identity:PasswordTries", "1"));
        var (first, _) = await app.LoginAsync(phone: Phone);
        await SetPasswordAsync(first, Good);
        await LoginAsync(app, "09127654321", "wrong");
        await LoginAsync(app, "09127654321", "wrong");

        var response = await LoginAsync(app, Phone, Good);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Rule", "SEC-09")]
    public async Task SEC09_one_address_cannot_try_many_logins()
    {
        await using var app = await StartAsync(postgres, ("Identity:PasswordTriesPerIp", "2"));
        var attacker = app.Browser("203.0.113.1");
        await attacker.PostAsJsonAsync("/api/auth/password", new { login = "nobody_1", password = Good }, Cancel);
        await attacker.PostAsJsonAsync("/api/auth/password", new { login = "nobody_2", password = Good }, Cancel);

        var third = await attacker.PostAsJsonAsync("/api/auth/password", new { login = "nobody_3", password = Good }, Cancel);
        var otherAddress = await app.Browser("203.0.113.2").PostAsJsonAsync("/api/auth/password", new { login = "nobody_3", password = Good }, Cancel);

        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Equal("rate_limited", await ErrorCodeAsync(third));
        Assert.Equal(HttpStatusCode.Unauthorized, otherAddress.StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-14")]
    public async Task AUTH14_a_login_longer_than_any_real_one_is_400()
    {
        await using var app = await StartAsync(postgres);

        var response = await app.Browser().PostAsJsonAsync("/api/auth/password", new { login = new string('a', 255), password = Good }, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- AUTH-15 username ----

    [Fact]
    [Trait("Rule", "AUTH-15")]
    public async Task AUTH15_set_a_username()
    {
        await using var app = await StartAsync(postgres);
        var (browser, _) = await app.LoginAsync();

        var response = await browser.PostAsJsonAsync("/api/account/username", new { username = "Sara_1" }, Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Sara_1", (await JsonAsync(response)).GetProperty("username").GetString());
        Assert.Equal("Sara_1", (await JsonAsync(await browser.GetAsync("/api/me", Cancel))).GetProperty("username").GetString());
    }

    [Theory]
    [Trait("Rule", "AUTH-15")]
    [InlineData("ab")]
    [InlineData("1sara")]
    [InlineData("sara!")]
    [InlineData("سارا_سارا")]
    [InlineData("a23456789012345678901234567890123")] // 33 characters
    [InlineData(null)]
    public async Task AUTH15_bad_shape_is_400(string? username)
    {
        await using var app = await StartAsync(postgres);
        var (browser, _) = await app.LoginAsync();

        var response = await browser.PostAsJsonAsync("/api/account/username", new { username }, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_username", await ErrorCodeAsync(response));
    }

    [Fact]
    [Trait("Rule", "AUTH-15")]
    public async Task AUTH15_taken_username_in_another_letter_case_is_409()
    {
        await using var app = await StartAsync(postgres);
        await app.AddUserAsync(username: "Sara_1");
        var (browser, _) = await app.LoginAsync();

        var response = await browser.PostAsJsonAsync("/api/account/username", new { username = "sara_1" }, Cancel);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("username_taken", await ErrorCodeAsync(response));
    }

    // ---- AUTH-04 and AUTH-16 ----

    [Fact]
    [Trait("Rule", "AUTH-04")]
    public async Task AUTH04_me_says_if_the_user_has_a_password_and_shows_no_hash()
    {
        await using var app = await StartAsync(postgres);
        var (browser, _) = await app.LoginAsync();
        var before = await JsonAsync(await browser.GetAsync("/api/me", Cancel));
        await SetPasswordAsync(browser, Good);

        var body = await (await browser.GetAsync("/api/me", Cancel)).Content.ReadAsStringAsync(Cancel);

        Assert.False(before.GetProperty("has_password").GetBoolean());
        Assert.Contains("\"has_password\":true", body, StringComparison.Ordinal);
        Assert.DoesNotContain("argon2", body, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "AUTH-16")]
    public async Task AUTH16_login_settings_need_no_session()
    {
        await using var app = await StartAsync(postgres, ("Identity:PasswordMinLength", "8"), ("Identity:MaxNameLength", "64"));

        var response = await app.Browser().GetAsync("/api/auth/settings", Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await JsonAsync(response);
        Assert.True(json.GetProperty("dev_login").GetBoolean());
        Assert.Equal(8, json.GetProperty("password_min_length").GetInt32());
        Assert.Equal(64, json.GetProperty("name_max_length").GetInt32());
    }

    [Fact]
    [Trait("Rule", "AUTH-16")]
    public async Task AUTH16_dev_login_is_false_when_the_setting_is_off()
    {
        await using var app = await StartAsync(postgres, ("Identity:DevLogin", "false"));

        var json = await JsonAsync(await app.Browser().GetAsync("/api/auth/settings", Cancel));

        Assert.False(json.GetProperty("dev_login").GetBoolean());
    }
}
