using System.Net;
using System.Net.Http.Json;
using Dalan.Identity.SmsLogin;
using Dalan.Testing;
using static Dalan.Identity.Tests.Http;

namespace Dalan.Identity.Tests;

public class SmsLoginTests(PostgresServer postgres)
{
    private const string Phone = "09121234567";
    private const string Users = "SELECT count(*) FROM identity.users";

    private static Task<HttpResponseMessage> VerifyAsync(HttpClient browser, string phone, string code, string? name = null) =>
        browser.PostAsJsonAsync("/api/auth/sms/verify", new { phone, code, name }, Cancel);

    /// <summary>A 6-digit code that is surely not the right one.</summary>
    private static string Other(string code) => code == "000000" ? "000001" : "000000";

    // ---- SEC-06 proof of work ----

    [Fact]
    [Trait("Rule", "SEC-06")]
    public async Task SEC06_challenge_has_the_difficulty_of_the_settings()
    {
        await using var app = await StartAsync(postgres, ("Identity:PowDifficultyBits", "16"));

        var response = await app.Browser().PostAsJsonAsync("/api/auth/challenge", new { phone = Phone }, Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await JsonAsync(response);
        Assert.Equal(16, json.GetProperty("difficulty").GetInt32());
        Assert.False(string.IsNullOrEmpty(json.GetProperty("challenge").GetString()));
    }

    [Fact]
    [Trait("Rule", "SEC-06")]
    public void SEC06_zero_bits_are_counted_from_the_first_bit_of_the_hash()
    {
        // SHA-256("abc.0") = 20b699b1...: the first byte 0x20 is 0010 0000, so 2 zero bits.
        Assert.True(Proof.IsSolved("abc", "0", difficulty: 2));
        Assert.False(Proof.IsSolved("abc", "0", difficulty: 3));
    }

    [Fact]
    [Trait("Rule", "SEC-06")]
    public async Task SEC06_no_work_means_no_sms()
    {
        await using var app = await StartAsync(postgres, ("Identity:PowDifficultyBits", "12"));
        var browser = app.Browser();
        var challenge = (await JsonAsync(await browser.PostAsJsonAsync("/api/auth/challenge", new { phone = Phone }, Cancel)))
            .GetProperty("challenge").GetString()!;
        var notSolved = Enumerable.Range(0, 100).Select(n => $"x{n}").First(nonce => !Proof.IsSolved(challenge, nonce, 12));

        var response = await browser.PostAsJsonAsync("/api/auth/sms", new { phone = Phone, challenge, nonce = notSolved }, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_proof", await ErrorCodeAsync(response));
        Assert.Empty(app.Sms.Sent);
    }

    [Fact]
    [Trait("Rule", "SEC-06")]
    public async Task SEC06_challenge_of_another_phone_is_refused()
    {
        await using var app = await StartAsync(postgres);
        var browser = app.Browser();
        var json = await JsonAsync(await browser.PostAsJsonAsync("/api/auth/challenge", new { phone = Phone }, Cancel));
        var challenge = json.GetProperty("challenge").GetString()!;
        var nonce = DalanApp.Solve(challenge, json.GetProperty("difficulty").GetInt32());

        var response = await browser.PostAsJsonAsync("/api/auth/sms", new { phone = "09127654321", challenge, nonce }, Cancel);

        Assert.Equal("bad_proof", await ErrorCodeAsync(response));
        Assert.Empty(app.Sms.Sent);
    }

    [Fact]
    [Trait("Rule", "SEC-06")]
    public async Task SEC06_old_challenge_is_refused()
    {
        await using var app = await StartAsync(postgres, ("Identity:PowChallengeSeconds", "120"));
        var browser = app.Browser();
        var json = await JsonAsync(await browser.PostAsJsonAsync("/api/auth/challenge", new { phone = Phone }, Cancel));
        var challenge = json.GetProperty("challenge").GetString()!;
        var nonce = DalanApp.Solve(challenge, json.GetProperty("difficulty").GetInt32());

        app.Time.Advance(TimeSpan.FromSeconds(120));
        var response = await browser.PostAsJsonAsync("/api/auth/sms", new { phone = Phone, challenge, nonce }, Cancel);

        Assert.Equal("bad_proof", await ErrorCodeAsync(response));
        Assert.Empty(app.Sms.Sent);
    }

    [Theory]
    [Trait("Rule", "SEC-06")]
    [InlineData("4102444800.c2FsdA.bm90LW91ci1tYWM")] // right shape, far-away expiry, a MAC we did not make
    [InlineData("nonsense")]
    [InlineData(null)]
    public async Task SEC06_made_up_challenge_is_refused(string? challenge)
    {
        await using var app = await StartAsync(postgres, ("Identity:PowDifficultyBits", "0"));

        var response = await app.Browser().PostAsJsonAsync("/api/auth/sms", new { phone = Phone, challenge, nonce = "0" }, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_proof", await ErrorCodeAsync(response));
        Assert.Empty(app.Sms.Sent);
    }

    // ---- AUTH-11 ask for a code ----

    [Fact]
    [Trait("Rule", "AUTH-11")]
    public async Task AUTH11_code_is_sent_to_the_number()
    {
        await using var app = await StartAsync(postgres);

        var response = await DalanApp.AskCodeAsync(app.Browser(), Phone);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await JsonAsync(response);
        Assert.Equal(120, json.GetProperty("expires_in").GetInt32());
        Assert.Equal(60, json.GetProperty("retry_in").GetInt32());
        Assert.Equal(6, json.GetProperty("code_digits").GetInt32());
        var sms = Assert.Single(app.Sms.Sent);
        Assert.Equal("+989121234567", sms.Phone);
        Assert.Matches("^[0-9]{6}$", sms.Code);
    }

    [Fact]
    [Trait("Rule", "AUTH-11")]
    public async Task AUTH11_answer_is_the_same_for_a_known_and_an_unknown_number()
    {
        await using var app = await StartAsync(postgres);
        await app.AddUserAsync(phone: "+989121234567");

        var known = await DalanApp.AskCodeAsync(app.Browser(), Phone);
        var unknown = await DalanApp.AskCodeAsync(app.Browser(), "09127654321");

        Assert.Equal(await known.Content.ReadAsStringAsync(Cancel), await unknown.Content.ReadAsStringAsync(Cancel));
    }

    [Fact]
    [Trait("Rule", "AUTH-11")]
    public async Task AUTH11_code_is_not_stored_in_clear()
    {
        await using var app = await StartAsync(postgres);

        await DalanApp.AskCodeAsync(app.Browser(), Phone);

        var code = app.Sms.Sent[0].Code;
        Assert.Equal(1L, await PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT count(*) FROM identity.sms_codes"));
        Assert.Equal(32, await PostgresServer.ScalarAsync<int>(app.ConnectionString, "SELECT length(code_hash) FROM identity.sms_codes"));
        Assert.Equal(0L, await PostgresServer.ScalarAsync<long>(app.ConnectionString,
            $"SELECT count(*) FROM identity.sms_codes c WHERE c::text LIKE '%{code}%' OR encode(code_hash, 'escape') LIKE '%{code}%'"));
    }

    [Theory]
    [Trait("Rule", "AUTH-11")]
    [InlineData("/api/auth/challenge")]
    [InlineData("/api/auth/sms")]
    [InlineData("/api/auth/sms/verify")]
    public async Task AUTH11_not_a_mobile_number_is_400(string path)
    {
        await using var app = await StartAsync(postgres);

        var response = await app.Browser().PostAsJsonAsync(path, new { phone = "02112345678", challenge = "x", nonce = "0", code = "123456" }, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_phone", await ErrorCodeAsync(response));
        Assert.Empty(app.Sms.Sent);
    }

    [Fact]
    [Trait("Rule", "AUTH-11")]
    public async Task AUTH11_failed_send_is_503_and_does_not_count()
    {
        await using var app = await StartAsync(postgres);
        app.Sms.Fails = true;

        var failed = await DalanApp.AskCodeAsync(app.Browser(), Phone);
        app.Sms.Fails = false;
        var again = await DalanApp.AskCodeAsync(app.Browser(), Phone);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        Assert.Equal("sms_unavailable", await ErrorCodeAsync(failed));
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    // ---- SEC-07 limits per phone ----

    [Fact]
    [Trait("Rule", "SEC-07")]
    public async Task SEC07_second_sms_too_soon_is_429_with_the_seconds_left()
    {
        await using var app = await StartAsync(postgres, ("Identity:SmsWaitSeconds", "60"));
        await DalanApp.AskCodeAsync(app.Browser(), Phone);

        app.Time.Advance(TimeSpan.FromSeconds(20));
        var response = await DalanApp.AskCodeAsync(app.Browser(), Phone);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("sms_wait", await ErrorCodeAsync(response));
        Assert.Equal("40", Assert.Single(response.Headers.GetValues("Retry-After")));
        Assert.Single(app.Sms.Sent);
    }

    [Fact]
    [Trait("Rule", "SEC-07")]
    public async Task SEC07_after_the_wait_an_sms_is_sent_again()
    {
        await using var app = await StartAsync(postgres, ("Identity:SmsWaitSeconds", "60"));
        await DalanApp.AskCodeAsync(app.Browser(), Phone);

        app.Time.Advance(TimeSpan.FromSeconds(60));
        var response = await DalanApp.AskCodeAsync(app.Browser(), Phone);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, app.Sms.Sent.Count);
    }

    [Fact]
    [Trait("Rule", "SEC-07")]
    public async Task SEC07_daily_limit_per_number_and_a_day_later()
    {
        await using var app = await StartAsync(postgres, ("Identity:SmsPerPhonePerDay", "5"));
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await DalanApp.AskCodeAsync(app.Browser(), Phone)).StatusCode);
            app.Time.Advance(TimeSpan.FromMinutes(1));
        }

        var sixth = await DalanApp.AskCodeAsync(app.Browser(), Phone);
        app.Time.Advance(TimeSpan.FromHours(24));
        var nextDay = await DalanApp.AskCodeAsync(app.Browser(), Phone);

        Assert.Equal(HttpStatusCode.TooManyRequests, sixth.StatusCode);
        Assert.Equal("sms_limit", await ErrorCodeAsync(sixth));
        Assert.Equal(HttpStatusCode.OK, nextDay.StatusCode);
        Assert.Equal(6, app.Sms.Sent.Count);
    }

    [Fact]
    [Trait("Rule", "SEC-07")]
    public async Task SEC07_one_number_does_not_use_up_another_numbers_limit()
    {
        await using var app = await StartAsync(postgres);
        await DalanApp.AskCodeAsync(app.Browser(), Phone);

        var other = await DalanApp.AskCodeAsync(app.Browser(), "09127654321");

        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
    }

    // ---- SEC-08 limit per address and the budget ----

    [Fact]
    [Trait("Rule", "SEC-08")]
    public async Task SEC08_limit_per_address()
    {
        await using var app = await StartAsync(postgres, ("Identity:SmsPerIpPerDay", "2"));
        await DalanApp.AskCodeAsync(app.Browser("203.0.113.1"), "09120000001");
        await DalanApp.AskCodeAsync(app.Browser("203.0.113.1"), "09120000002");

        var sameAddress = await DalanApp.AskCodeAsync(app.Browser("203.0.113.1"), "09120000003");
        var otherAddress = await DalanApp.AskCodeAsync(app.Browser("203.0.113.2"), "09120000003");

        Assert.Equal(HttpStatusCode.TooManyRequests, sameAddress.StatusCode);
        Assert.Equal("sms_limit", await ErrorCodeAsync(sameAddress));
        Assert.Equal(HttpStatusCode.OK, otherAddress.StatusCode);
    }

    [Fact]
    [Trait("Rule", "SEC-08")]
    public async Task SEC08_used_up_budget_is_503_and_an_error_in_the_log()
    {
        await using var app = await StartAsync(postgres, ("Identity:SmsPerDay", "2"));
        await DalanApp.AskCodeAsync(app.Browser("203.0.113.1"), "09120000001");
        await DalanApp.AskCodeAsync(app.Browser("203.0.113.2"), "09120000002");

        var response = await DalanApp.AskCodeAsync(app.Browser("203.0.113.3"), "09120000003");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("sms_unavailable", await ErrorCodeAsync(response));
        Assert.Equal(2, app.Sms.Sent.Count);
        Assert.Contains(app.Logs.Lines, line =>
            line.Level == Microsoft.Extensions.Logging.LogLevel.Error && line.Text.Contains("Identity:SmsPerDay", StringComparison.Ordinal));
    }

    // ---- AUTH-12 log in with the code ----

    [Fact]
    [Trait("Rule", "AUTH-12")]
    public async Task AUTH12_known_user_logs_in_and_keeps_the_name()
    {
        await using var app = await StartAsync(postgres);
        var userId = await app.AddUserAsync("Sara", phone: "+989121234567");
        var browser = app.Browser();
        await DalanApp.AskCodeAsync(browser, Phone);

        var response = await VerifyAsync(browser, Phone, app.Sms.Sent[0].Code, name: "Other");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(SetCookie(response, SessionCookie));
        Assert.NotNull(SetCookie(response, RefreshCookie));
        var me = await JsonAsync(await browser.GetAsync("/api/me", Cancel));
        Assert.Equal(userId, me.GetProperty("id").GetInt64());
        Assert.Equal("Sara", me.GetProperty("name").GetString());
        Assert.DoesNotContain("9121234567", await response.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "AUTH-12")]
    public async Task AUTH12_wrong_code_is_400_and_sets_no_cookie()
    {
        await using var app = await StartAsync(postgres);
        await app.AddUserAsync(phone: "+989121234567");
        var browser = app.Browser();
        await DalanApp.AskCodeAsync(browser, Phone);

        var response = await VerifyAsync(browser, Phone, Other(app.Sms.Sent[0].Code));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("wrong_code", await ErrorCodeAsync(response));
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    [Trait("Rule", "AUTH-12")]
    public async Task AUTH12_code_without_any_sms_is_400()
    {
        await using var app = await StartAsync(postgres);

        var response = await VerifyAsync(app.Browser(), Phone, "123456", "Sara");

        Assert.Equal("wrong_code", await ErrorCodeAsync(response));
        Assert.Equal(0L, await PostgresServer.ScalarAsync<long>(app.ConnectionString, Users));
    }

    [Fact]
    [Trait("Rule", "AUTH-12")]
    public async Task AUTH12_expired_code_is_400()
    {
        await using var app = await StartAsync(postgres, ("Identity:SmsCodeSeconds", "120"));
        await app.AddUserAsync(phone: "+989121234567");
        var browser = app.Browser();
        await DalanApp.AskCodeAsync(browser, Phone);

        app.Time.Advance(TimeSpan.FromSeconds(120));
        var response = await VerifyAsync(browser, Phone, app.Sms.Sent[0].Code);

        Assert.Equal("wrong_code", await ErrorCodeAsync(response));
    }

    [Fact]
    [Trait("Rule", "AUTH-12")]
    public async Task AUTH12_after_too_many_wrong_tries_the_right_code_fails_too()
    {
        await using var app = await StartAsync(postgres, ("Identity:SmsCodeTries", "5"));
        await app.AddUserAsync(phone: "+989121234567");
        var browser = app.Browser();
        await DalanApp.AskCodeAsync(browser, Phone);
        var code = app.Sms.Sent[0].Code;
        for (var i = 0; i < 5; i++) await VerifyAsync(browser, Phone, Other(code));

        var response = await VerifyAsync(browser, Phone, code);

        Assert.Equal("wrong_code", await ErrorCodeAsync(response));
    }

    [Fact]
    [Trait("Rule", "AUTH-12")]
    public async Task AUTH12_four_wrong_tries_still_leave_one_for_the_right_code()
    {
        await using var app = await StartAsync(postgres, ("Identity:SmsCodeTries", "5"));
        await app.AddUserAsync(phone: "+989121234567");
        var browser = app.Browser();
        await DalanApp.AskCodeAsync(browser, Phone);
        var code = app.Sms.Sent[0].Code;
        for (var i = 0; i < 4; i++) await VerifyAsync(browser, Phone, Other(code));

        Assert.Equal(HttpStatusCode.OK, (await VerifyAsync(browser, Phone, code)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-12")]
    public async Task AUTH12_code_works_once()
    {
        await using var app = await StartAsync(postgres);
        await app.AddUserAsync(phone: "+989121234567");
        await DalanApp.AskCodeAsync(app.Browser(), Phone);
        var code = app.Sms.Sent[0].Code;
        Assert.Equal(HttpStatusCode.OK, (await VerifyAsync(app.Browser(), Phone, code)).StatusCode);

        var again = await VerifyAsync(app.Browser(), Phone, code);

        Assert.Equal("wrong_code", await ErrorCodeAsync(again));
    }

    [Fact]
    [Trait("Rule", "AUTH-12")]
    public async Task AUTH12_only_the_newest_code_works()
    {
        await using var app = await StartAsync(postgres, ("Identity:SmsWaitSeconds", "60"), ("Identity:SmsCodeSeconds", "120"));
        await app.AddUserAsync(phone: "+989121234567");
        await DalanApp.AskCodeAsync(app.Browser(), Phone);
        app.Time.Advance(TimeSpan.FromSeconds(60));
        await DalanApp.AskCodeAsync(app.Browser(), Phone);
        var (first, second) = (app.Sms.Sent[0].Code, app.Sms.Sent[1].Code);
        Assert.SkipWhen(first == second, "Both random codes are the same (one in a million).");

        var response = await VerifyAsync(app.Browser(), Phone, first);

        Assert.Equal("wrong_code", await ErrorCodeAsync(response));
    }

    [Fact]
    [Trait("Rule", "AUTH-12")]
    public async Task AUTH12_new_number_needs_a_name_and_the_code_stays_valid()
    {
        await using var app = await StartAsync(postgres, ("Identity:SmsCodeTries", "2"));
        var browser = app.Browser();
        await DalanApp.AskCodeAsync(browser, Phone);
        var code = app.Sms.Sent[0].Code;

        // More calls than SmsCodeTries: a right code without a name must not use up the tries.
        HttpResponseMessage noName = null!;
        for (var i = 0; i < 3; i++) noName = await VerifyAsync(browser, Phone, code);
        var usersBefore = await PostgresServer.ScalarAsync<long>(app.ConnectionString, Users);
        var withName = await VerifyAsync(browser, Phone, code, "سارا");

        Assert.Equal(HttpStatusCode.BadRequest, noName.StatusCode);
        Assert.Equal("name_required", await ErrorCodeAsync(noName));
        Assert.Equal(0L, usersBefore);
        Assert.Equal(HttpStatusCode.OK, withName.StatusCode);
        Assert.NotNull(SetCookie(withName, RefreshCookie));
        Assert.Equal("سارا", (await JsonAsync(withName)).GetProperty("name").GetString());
        Assert.Equal("+989121234567", await PostgresServer.ScalarAsync<string>(app.ConnectionString, "SELECT phone FROM identity.users"));
    }

    [Fact]
    [Trait("Rule", "AUTH-12")]
    public async Task AUTH12_new_number_with_a_too_long_name_is_name_required()
    {
        await using var app = await StartAsync(postgres, ("Identity:MaxNameLength", "64"));
        var browser = app.Browser();
        await DalanApp.AskCodeAsync(browser, Phone);

        var response = await VerifyAsync(browser, Phone, app.Sms.Sent[0].Code, new string('x', 65));

        Assert.Equal("name_required", await ErrorCodeAsync(response));
    }

    [Fact]
    [Trait("Rule", "SEC-05")]
    public async Task SEC05_sms_calls_as_a_form_post_are_refused()
    {
        await using var app = await StartAsync(postgres);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["phone"] = Phone });

        var response = await app.Browser().PostAsync("/api/auth/challenge", form, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
