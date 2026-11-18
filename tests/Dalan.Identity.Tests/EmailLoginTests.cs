using System.Net;
using System.Net.Http.Json;
using Dalan.Testing;
using static Dalan.Identity.Tests.Http;

namespace Dalan.Identity.Tests;

/// <summary>Level 1: login by email code, and the step to level 2 (a verified phone).</summary>
public class EmailLoginTests(PostgresServer postgres)
{
    private const string Email = "sara@example.com";
    private const string Phone = "09121234567";

    private static Task<HttpResponseMessage> VerifyAsync(HttpClient browser, string email, string code, string? name = null) =>
        browser.PostAsJsonAsync("/api/auth/email/verify", new { email, code, name }, Cancel);

    private static Task<HttpResponseMessage> AddPhoneAsync(HttpClient browser, string phone, string code) =>
        browser.PostAsJsonAsync("/api/account/phone", new { phone, code }, Cancel);

    private static string Other(string code) => code == "000000" ? "000001" : "000000";

    // ---- AUTH-24 ask for an email code ----

    [Fact]
    [Trait("Rule", "AUTH-24")]
    public async Task AUTH24_code_is_sent_to_the_address_in_lower_case()
    {
        await using var app = await StartAsync(postgres);

        var response = await DalanApp.AskEmailCodeAsync(app.Browser(), " Sara@Example.com ");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await JsonAsync(response);
        Assert.Equal(600, json.GetProperty("expires_in").GetInt32());
        Assert.Equal(60, json.GetProperty("retry_in").GetInt32());
        Assert.Equal(6, json.GetProperty("code_digits").GetInt32());
        var email = Assert.Single(app.Email.Sent);
        Assert.Equal(Email, email.Email);
        Assert.Matches("^[0-9]{6}$", email.Code);
    }

    [Fact]
    [Trait("Rule", "AUTH-24")]
    public async Task AUTH24_code_is_not_stored_in_clear()
    {
        await using var app = await StartAsync(postgres);
        await DalanApp.AskEmailCodeAsync(app.Browser(), Email);
        var code = app.Email.Sent[0].Code;

        var found = await PostgresServer.ScalarAsync<long>(app.ConnectionString,
            $"SELECT count(*) FROM identity.email_codes c WHERE c::text LIKE '%{code}%'");

        Assert.Equal(0, found);
    }

    [Theory]
    [Trait("Rule", "AUTH-24")]
    [InlineData("sara.example.com")]
    [InlineData("sara@example")]
    [InlineData("sa ra@example.com")]
    public async Task AUTH24_not_an_email_is_refused(string email)
    {
        await using var app = await StartAsync(postgres);

        var response = await app.Browser().PostAsJsonAsync("/api/auth/email", new { email, challenge = "x", nonce = "0" }, Cancel);
        var challenge = await app.Browser().PostAsJsonAsync("/api/auth/challenge", new { email }, Cancel);

        Assert.Equal("bad_email", await ErrorCodeAsync(response));
        Assert.Equal("bad_email", await ErrorCodeAsync(challenge));
        Assert.Empty(app.Email.Sent);
    }

    [Fact]
    [Trait("Rule", "AUTH-24")]
    public async Task AUTH24_failed_send_is_503_and_does_not_count()
    {
        await using var app = await StartAsync(postgres);
        app.Email.Fails = true;

        var failed = await DalanApp.AskEmailCodeAsync(app.Browser(), Email);
        app.Email.Fails = false;
        var again = await DalanApp.AskEmailCodeAsync(app.Browser(), Email);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        Assert.Equal("email_unavailable", await ErrorCodeAsync(failed));
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    // ---- AUTH-25 log in with the email code ----

    [Fact]
    [Trait("Rule", "AUTH-25")]
    public async Task AUTH25_new_address_needs_a_name_then_makes_a_user_with_no_phone()
    {
        await using var app = await StartAsync(postgres);
        var browser = app.Browser();
        await DalanApp.AskEmailCodeAsync(browser, Email);
        var code = app.Email.Sent[0].Code;

        var noName = await VerifyAsync(browser, Email, code);
        var withName = await VerifyAsync(browser, Email, code, "Sara");

        Assert.Equal("name_required", await ErrorCodeAsync(noName));
        Assert.Equal(HttpStatusCode.OK, withName.StatusCode);
        Assert.NotNull(SetCookie(withName, SessionCookie));
        Assert.NotNull(SetCookie(withName, RefreshCookie));
        Assert.Equal(1, await PostgresServer.ScalarAsync<long>(app.ConnectionString,
            $"SELECT count(*) FROM identity.users WHERE email = '{Email}' AND phone IS NULL AND display_name = 'Sara'"));
    }

    [Fact]
    [Trait("Rule", "AUTH-25")]
    public async Task AUTH25_known_user_logs_in_again()
    {
        await using var app = await StartAsync(postgres);
        await app.LoginByEmailAsync(Email);
        var browser = app.Browser();
        app.Time.Advance(TimeSpan.FromSeconds(60));
        await DalanApp.AskEmailCodeAsync(browser, Email);

        var response = await VerifyAsync(browser, Email, app.Email.Sent[^1].Code);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Sara", (await JsonAsync(response)).GetProperty("name").GetString());
        Assert.Equal(1, await PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT count(*) FROM identity.users"));
    }

    [Fact]
    [Trait("Rule", "AUTH-25")]
    public async Task AUTH25_wrong_expired_and_used_codes_are_refused()
    {
        await using var app = await StartAsync(postgres);
        var browser = app.Browser();
        await DalanApp.AskEmailCodeAsync(browser, Email);
        var code = app.Email.Sent[0].Code;

        var wrong = await VerifyAsync(browser, Email, Other(code), "Sara");
        Assert.Equal("wrong_code", await ErrorCodeAsync(wrong));
        Assert.Null(SetCookie(wrong, SessionCookie));

        (await VerifyAsync(browser, Email, code, "Sara")).EnsureSuccessStatusCode();
        Assert.Equal("wrong_code", await ErrorCodeAsync(await VerifyAsync(browser, Email, code, "Sara")));

        app.Time.Advance(TimeSpan.FromSeconds(60));
        await DalanApp.AskEmailCodeAsync(browser, Email);
        app.Time.Advance(TimeSpan.FromSeconds(600));
        Assert.Equal("wrong_code", await ErrorCodeAsync(await VerifyAsync(browser, Email, app.Email.Sent[^1].Code)));
    }

    // ---- AUTH-01 and AUTH-04 ----

    [Fact]
    [Trait("Rule", "AUTH-01")]
    public async Task AUTH01_same_email_twice_fails()
    {
        await using var app = await StartAsync(postgres);
        await PostgresServer.ScalarAsync<int>(app.ConnectionString,
            $"INSERT INTO identity.users (email, display_name, created_at) VALUES ('{Email}', 'A', now()) RETURNING 1");

        await Assert.ThrowsAnyAsync<Exception>(() => PostgresServer.ScalarAsync<int>(app.ConnectionString,
            $"INSERT INTO identity.users (email, display_name, created_at) VALUES ('{Email}', 'B', now()) RETURNING 1"));
    }

    [Fact]
    [Trait("Rule", "AUTH-04")]
    public async Task AUTH04_email_user_sees_the_email_and_no_phone()
    {
        await using var app = await StartAsync(postgres);
        var browser = await app.LoginByEmailAsync(Email);

        var me = await JsonAsync(await browser.GetAsync("/api/me", Cancel));

        Assert.Equal(Email, me.GetProperty("email").GetString());
        Assert.False(me.GetProperty("has_phone").GetBoolean());
    }

    // ---- AUTH-13, AUTH-14 ----

    [Fact]
    [Trait("Rule", "AUTH-13")]
    public async Task AUTH13_forgot_the_password_login_by_email_sets_a_new_one()
    {
        await using var app = await StartAsync(postgres);
        var first = await app.LoginByEmailAsync(Email);
        (await first.PostAsJsonAsync("/api/account/password", new { password = "the old password" }, Cancel)).EnsureSuccessStatusCode();
        app.Time.Advance(TimeSpan.FromSeconds(60));
        var byEmail = await app.LoginByEmailAsync(Email);

        var response = await byEmail.PostAsJsonAsync("/api/account/password", new { password = "a new password" }, Cancel);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-14")]
    public async Task AUTH14_login_by_email_in_any_case()
    {
        await using var app = await StartAsync(postgres);
        var browser = await app.LoginByEmailAsync(Email);
        (await browser.PostAsJsonAsync("/api/account/password", new { password = "correct horse" }, Cancel)).EnsureSuccessStatusCode();

        var response = await NoJar(app).PostAsJsonAsync("/api/auth/password", new { login = "Sara@Example.com", password = "correct horse" }, Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- AUTH-16, AUTH-17 ----

    [Fact]
    [Trait("Rule", "AUTH-16")]
    public async Task AUTH16_settings_say_if_email_login_is_on()
    {
        await using var app = await StartAsync(postgres);

        var json = await JsonAsync(await app.Browser().GetAsync("/api/auth/settings", Cancel));

        Assert.True(json.GetProperty("email_login").GetBoolean());
    }

    [Fact]
    [Trait("Rule", "AUTH-17")]
    public async Task AUTH17_no_code_or_whole_email_in_the_log()
    {
        await using var app = await StartAsync(postgres);

        await app.LoginByEmailAsync(Email);

        var log = string.Join('\n', app.Logs.Lines.Select(line => line.Text));
        Assert.Contains("Security event email_sent: to sa***@example.com", log, StringComparison.Ordinal);
        Assert.Contains("by email", log, StringComparison.Ordinal);
        Assert.DoesNotContain(app.Email.Sent[0].Code, log, StringComparison.Ordinal);
        Assert.DoesNotContain(Email, log, StringComparison.Ordinal);
    }

    // ---- AUTH-19, AUTH-26 level 2 ----

    [Fact]
    [Trait("Rule", "AUTH-19")]
    public async Task AUTH19_no_phone_no_builder_request()
    {
        await using var app = await StartAsync(postgres);
        var browser = await app.LoginByEmailAsync(Email);

        var response = await browser.PostAsync("/api/account/builder", null, Cancel);
        var me = await JsonAsync(await browser.GetAsync("/api/me", Cancel));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("phone_required", await ErrorCodeAsync(response));
        Assert.Equal("none", me.GetProperty("builder").GetString());
    }

    [Fact]
    [Trait("Rule", "AUTH-26")]
    public async Task AUTH26_add_a_phone_then_log_in_by_sms_and_ask_to_build()
    {
        await using var app = await StartAsync(postgres);
        var browser = await app.LoginByEmailAsync(Email);
        (await DalanApp.AskCodeAsync(browser, Phone)).EnsureSuccessStatusCode();

        var response = await AddPhoneAsync(browser, Phone, app.Sms.Sent[^1].Code);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await JsonAsync(response)).GetProperty("has_phone").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await browser.PostAsync("/api/account/builder", null, Cancel)).StatusCode);
        app.Time.Advance(TimeSpan.FromSeconds(60));
        var bySms = await app.LoginBySmsAsync(Phone);
        Assert.Equal(Email, (await JsonAsync(await bySms.GetAsync("/api/me", Cancel))).GetProperty("email").GetString());
    }

    [Fact]
    [Trait("Rule", "AUTH-26")]
    public async Task AUTH26_wrong_code_adds_no_phone()
    {
        await using var app = await StartAsync(postgres);
        var browser = await app.LoginByEmailAsync(Email);
        (await DalanApp.AskCodeAsync(browser, Phone)).EnsureSuccessStatusCode();

        var response = await AddPhoneAsync(browser, Phone, Other(app.Sms.Sent[^1].Code));

        Assert.Equal("wrong_code", await ErrorCodeAsync(response));
        Assert.False((await JsonAsync(await browser.GetAsync("/api/me", Cancel))).GetProperty("has_phone").GetBoolean());
    }

    [Fact]
    [Trait("Rule", "AUTH-26")]
    public async Task AUTH26_phone_of_another_user_is_taken()
    {
        await using var app = await StartAsync(postgres);
        await app.AddUserAsync(phone: "+989121234567");
        var browser = await app.LoginByEmailAsync(Email);
        (await DalanApp.AskCodeAsync(browser, Phone)).EnsureSuccessStatusCode();

        var response = await AddPhoneAsync(browser, Phone, app.Sms.Sent[^1].Code);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("phone_taken", await ErrorCodeAsync(response));
    }

    [Fact]
    [Trait("Rule", "AUTH-26")]
    public async Task AUTH26_user_with_a_phone_cannot_add_another()
    {
        await using var app = await StartAsync(postgres);
        var browser = await app.LoginBySmsAsync(Phone);
        (await DalanApp.AskCodeAsync(browser, "09127654321")).EnsureSuccessStatusCode();

        var response = await AddPhoneAsync(browser, "09127654321", app.Sms.Sent[^1].Code);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("has_phone", await ErrorCodeAsync(response));
    }

    // ---- SEC-06, SEC-13 ----

    [Fact]
    [Trait("Rule", "SEC-06")]
    public async Task SEC06_email_challenge_works_only_for_its_address()
    {
        await using var app = await StartAsync(postgres);
        var browser = app.Browser();
        var json = await JsonAsync(await browser.PostAsJsonAsync("/api/auth/challenge", new { email = Email }, Cancel));
        var challenge = json.GetProperty("challenge").GetString()!;
        var nonce = DalanApp.Solve(challenge, json.GetProperty("difficulty").GetInt32());

        var other = await browser.PostAsJsonAsync("/api/auth/email", new { email = "omid@example.com", challenge, nonce }, Cancel);
        var right = await browser.PostAsJsonAsync("/api/auth/email", new { email = Email, challenge, nonce }, Cancel);

        Assert.Equal("bad_proof", await ErrorCodeAsync(other));
        Assert.Equal(HttpStatusCode.OK, right.StatusCode);
        Assert.Equal(Email, Assert.Single(app.Email.Sent).Email);
    }

    [Fact]
    [Trait("Rule", "SEC-13")]
    public async Task SEC13_second_email_too_soon_waits()
    {
        await using var app = await StartAsync(postgres);
        await DalanApp.AskEmailCodeAsync(app.Browser(), Email);
        app.Time.Advance(TimeSpan.FromSeconds(20));

        var response = await DalanApp.AskEmailCodeAsync(app.Browser(), Email);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("email_wait", await ErrorCodeAsync(response));
        Assert.Equal("40", response.Headers.RetryAfter!.ToString());
    }

    [Fact]
    [Trait("Rule", "SEC-13")]
    public async Task SEC13_daily_limit_per_address()
    {
        await using var app = await StartAsync(postgres, ("Identity:EmailPerAddressPerDay", "2"));
        for (var i = 0; i < 2; i++)
        {
            (await DalanApp.AskEmailCodeAsync(app.Browser(), Email)).EnsureSuccessStatusCode();
            app.Time.Advance(TimeSpan.FromSeconds(60));
        }

        var response = await DalanApp.AskEmailCodeAsync(app.Browser(), Email);

        Assert.Equal("email_limit", await ErrorCodeAsync(response));
    }

    [Fact]
    [Trait("Rule", "SEC-13")]
    public async Task SEC13_email_budget_does_not_stop_sms()
    {
        await using var app = await StartAsync(postgres, ("Identity:EmailPerDay", "1"));
        (await DalanApp.AskEmailCodeAsync(app.Browser(), Email)).EnsureSuccessStatusCode();

        var email = await DalanApp.AskEmailCodeAsync(app.Browser(), "omid@example.com");
        var sms = await DalanApp.AskCodeAsync(app.Browser(), Phone);

        Assert.Equal("email_unavailable", await ErrorCodeAsync(email));
        Assert.Equal(HttpStatusCode.OK, sms.StatusCode);
        Assert.Contains(app.Logs.Lines, line =>
            line.Level == Microsoft.Extensions.Logging.LogLevel.Error && line.Text.Contains("Identity:EmailPerDay", StringComparison.Ordinal));
    }

    // ---- ADM-16 who asked for the email ----

    [Fact]
    [Trait("Rule", "ADM-16")]
    public async Task ADM16_sent_email_is_recorded_with_address_ip_port_and_time()
    {
        await using var app = await StartAsync(postgres);

        var response = await DalanApp.AskEmailCodeAsync(SmsLoginTests.ThroughProxy(app, "203.0.113.7:51000"), Email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([$"Email {Email} 203.0.113.7 51000"], await app.AccessLogAsync());
    }

    [Fact]
    [Trait("Rule", "ADM-16")]
    public async Task ADM16_refused_email_writes_no_row()
    {
        await using var app = await StartAsync(postgres);
        var browser = SmsLoginTests.ThroughProxy(app, "203.0.113.7:51000");

        await DalanApp.AskEmailCodeAsync(browser, Email);
        var tooSoon = await DalanApp.AskEmailCodeAsync(browser, Email);

        Assert.Equal("email_wait", await ErrorCodeAsync(tooSoon));
        Assert.Single(await app.AccessLogAsync());
    }
}
