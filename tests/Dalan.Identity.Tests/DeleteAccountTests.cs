using System.Net;
using System.Net.Http.Json;
using Dalan.Relay.Bots;
using Dalan.Testing;
using Microsoft.Extensions.DependencyInjection;
using static Dalan.Identity.Tests.Http;

namespace Dalan.Identity.Tests;

/// <summary>AUTH-36: a user deletes the own account.</summary>
public class DeleteAccountTests(PostgresServer postgres)
{
    private const string AdminPhone = "09129990001";
    private const string Phone = "09121234567";
    private const string PhoneE164 = "+989121234567";
    private const string Password = "a long password";

    private Task<DalanApp> StartAsync(params (string Key, string Value)[] settings) =>
        Http.StartAsync(postgres, [.. settings, ("Identity:AdminPhones", AdminPhone)]);

    private static async Task<long> IdAsync(HttpClient browser) =>
        (await JsonAsync(await browser.GetAsync("/api/me", Cancel))).GetProperty("id").GetInt64();

    private static Task<long> CountAsync(DalanApp app, string sql) => PostgresServer.ScalarAsync<long>(app.ConnectionString, sql);

    /// <summary>A logged-in user with a password, calling from a known address.</summary>
    private static async Task<(HttpClient Browser, long UserId)> UserWithPasswordAsync(DalanApp app, string phone = Phone)
    {
        var (browser, userId) = await app.LoginAsync("Sara", phone);
        DalanApp.From(browser, "5.6.7.8:41000");
        (await browser.PostAsJsonAsync("/api/account/password", new { password = Password }, Cancel)).EnsureSuccessStatusCode();
        return (browser, userId);
    }

    private static Task<HttpResponseMessage> DeleteAsync(HttpClient browser, object body) =>
        browser.PostAsJsonAsync("/api/account/delete", body, Cancel);

    /// <summary>Solves the challenge and asks for the code, like the client.</summary>
    private static async Task<(string Channel, HttpResponseMessage Response)> AskCodeAsync(HttpClient browser)
    {
        var answer = await browser.PostAsync("/api/account/delete/challenge", null, Cancel);
        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        var json = await JsonAsync(answer);
        var challenge = json.GetProperty("challenge").GetString()!;
        var nonce = DalanApp.Solve(challenge, json.GetProperty("difficulty").GetInt32());
        var response = await browser.PostAsJsonAsync("/api/account/delete/code", new { challenge, nonce }, Cancel);
        return (json.GetProperty("channel").GetString()!, response);
    }

    [Fact]
    [Trait("Rule", "AUTH-36")]
    public async Task AUTH36_owns_a_bot_gets_409_and_nothing_is_deleted()
    {
        await using var app = await StartAsync();
        var (browser, userId) = await UserWithPasswordAsync(app);
        await app.Services.GetRequiredService<BotRegistry>().CreateAsync(userId, "weather_bot", "Weather", Cancel);

        var response = await DeleteAsync(browser, new { password = Password, word = "DELETE" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("owns_things", await ErrorCodeAsync(response));
        Assert.Equal(1, await CountAsync(app, $"SELECT count(*) FROM identity.users WHERE id = {userId}"));
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/me", Cancel)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-36")]
    public async Task AUTH36_owns_a_site_gets_409()
    {
        await using var app = await StartAsync();
        var (browser, userId) = await UserWithPasswordAsync(app);
        await PostgresServer.ExecuteAsync(app.ConnectionString,
            $"INSERT INTO sites.sites (owner_user_id, name, created_at) VALUES ({userId}, 'mysite', now())");

        var response = await DeleteAsync(browser, new { password = Password, word = "DELETE" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("owns_things", await ErrorCodeAsync(response));
        Assert.Equal(1, await CountAsync(app, $"SELECT count(*) FROM identity.users WHERE id = {userId}"));
    }

    [Fact]
    [Trait("Rule", "AUTH-36")]
    public async Task AUTH36_no_password_proves_with_an_sms_code_to_the_own_phone()
    {
        await using var app = await StartAsync();
        var browser = DalanApp.From(await app.LoginBySmsAsync(Phone), "5.6.7.8:41000");
        var userId = await IdAsync(browser);
        var sent = app.Sms.Sent.Count;
        app.Time.Advance(TimeSpan.FromSeconds(121));

        var (channel, response) = await AskCodeAsync(browser);

        Assert.Equal("sms", channel);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(sent + 1, app.Sms.Sent.Count);
        Assert.Equal(PhoneE164, app.Sms.Sent[^1].Phone);
        Assert.Contains($"Phone {PhoneE164} 5.6.7.8 41000", await app.AccessLogAsync());

        var deleted = await DeleteAsync(browser, new { code = app.Sms.Sent[^1].Code, word = "DELETE" });

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(0, await CountAsync(app, $"SELECT count(*) FROM identity.users WHERE id = {userId}"));
    }

    [Fact]
    [Trait("Rule", "AUTH-36")]
    public async Task AUTH36_no_phone_proves_with_an_email_code()
    {
        await using var app = await StartAsync();
        var browser = DalanApp.From(await app.LoginByEmailAsync("sara@example.com"), "5.6.7.8:41000");
        var userId = await IdAsync(browser);
        app.Time.Advance(TimeSpan.FromSeconds(61));

        var (channel, response) = await AskCodeAsync(browser);

        Assert.Equal("email", channel);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("sara@example.com", app.Email.Sent[^1].Email);
        Assert.Contains("Email sara@example.com 5.6.7.8 41000", await app.AccessLogAsync());

        var deleted = await DeleteAsync(browser, new { code = app.Email.Sent[^1].Code, word = "حذف" });

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(0, await CountAsync(app, $"SELECT count(*) FROM identity.users WHERE id = {userId}"));
    }

    [Fact]
    [Trait("Rule", "AUTH-36")]
    public async Task AUTH36_wrong_code_or_unsolved_challenge_is_refused()
    {
        await using var app = await StartAsync();
        var browser = await app.LoginBySmsAsync(Phone);
        var challenge = (await JsonAsync(await browser.PostAsync("/api/account/delete/challenge", null, Cancel)))
            .GetProperty("challenge").GetString();

        var unsolved = await browser.PostAsJsonAsync("/api/account/delete/code", new { challenge, nonce = "x" }, Cancel);
        var wrong = await DeleteAsync(browser, new { code = "000000x", word = "DELETE" });

        Assert.Equal(HttpStatusCode.BadRequest, unsolved.StatusCode);
        Assert.Equal("bad_proof", await ErrorCodeAsync(unsolved));
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal("bad_proof", await ErrorCodeAsync(wrong));
        Assert.Equal(1, await CountAsync(app, "SELECT count(*) FROM identity.users"));
    }

    [Fact]
    [Trait("Rule", "AUTH-36")]
    public async Task AUTH36_admin_gets_403()
    {
        await using var app = await StartAsync();
        var (admin, adminId) = await app.LoginAsync("Owner", AdminPhone);

        var response = await DeleteAsync(admin, new { word = "DELETE" });
        var challenge = await admin.PostAsync("/api/account/delete/challenge", null, Cancel);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("admin_delete", await ErrorCodeAsync(response));
        Assert.Equal(HttpStatusCode.Forbidden, challenge.StatusCode);
        Assert.Equal("admin_delete", await ErrorCodeAsync(challenge));
        Assert.Equal(1, await CountAsync(app, $"SELECT count(*) FROM identity.users WHERE id = {adminId}"));
    }

    [Fact]
    [Trait("Rule", "AUTH-36")]
    public async Task AUTH36_deleted_account_is_gone_and_the_legal_records_stay()
    {
        await using var app = await StartAsync();
        var (browser, userId) = await UserWithPasswordAsync(app);
        (await browser.PostAsJsonAsync("/api/account/keys", new { name = "laptop" }, Cancel)).EnsureSuccessStatusCode();
        var accessRows = await CountAsync(app, $"SELECT count(*) FROM admin.access_log WHERE actor_id = {userId}");
        Assert.True(accessRows > 0);

        var response = await DeleteAsync(browser, new { password = Password, word = " delete " });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await CountAsync(app, $"SELECT count(*) FROM identity.users WHERE id = {userId}"));
        Assert.Equal(0, await CountAsync(app, "SELECT count(*) FROM identity.sessions"));
        Assert.Equal(0, await CountAsync(app, "SELECT count(*) FROM identity.access_keys"));
        Assert.Equal(accessRows, await CountAsync(app, $"SELECT count(*) FROM admin.access_log WHERE actor_id = {userId}"));
        Assert.Equal(1, await CountAsync(app, $"SELECT count(*) FROM admin.audit_log WHERE action = 'account.delete' AND user_id = {userId}"));
        // Its sessions fail at once, not only at the next refresh.
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/me", Cancel)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.PostAsync("/api/auth/refresh", null, Cancel)).StatusCode);

        var (again, againId) = await app.LoginAsync("Sara again", Phone);
        Assert.NotEqual(userId, againId);
        Assert.Equal(HttpStatusCode.OK, (await again.GetAsync("/api/me", Cancel)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-36")]
    public async Task AUTH36_wrong_password_word_or_missing_password_gets_400()
    {
        await using var app = await StartAsync();
        var (browser, userId) = await UserWithPasswordAsync(app);
        (await AskCodeAsync(browser)).Response.EnsureSuccessStatusCode();

        var wrongPassword = await DeleteAsync(browser, new { password = "not the password", word = "DELETE" });
        var wrongWord = await DeleteAsync(browser, new { password = Password, word = "DELET" });
        var codeInstead = await DeleteAsync(browser, new { code = app.Sms.Sent[^1].Code, word = "DELETE" });

        Assert.Equal(HttpStatusCode.BadRequest, wrongPassword.StatusCode);
        Assert.Equal("bad_proof", await ErrorCodeAsync(wrongPassword));
        Assert.Equal(HttpStatusCode.BadRequest, wrongWord.StatusCode);
        Assert.Equal("bad_word", await ErrorCodeAsync(wrongWord));
        Assert.Equal(HttpStatusCode.BadRequest, codeInstead.StatusCode);
        Assert.Equal("bad_proof", await ErrorCodeAsync(codeInstead));
        Assert.Equal(1, await CountAsync(app, $"SELECT count(*) FROM identity.users WHERE id = {userId}"));
    }

    [Fact]
    [Trait("Rule", "AUTH-36")]
    public async Task AUTH36_the_confirm_words_are_a_setting()
    {
        await using var app = await StartAsync(("Identity:DeleteConfirmWords", "REMOVE"));
        var (browser, userId) = await UserWithPasswordAsync(app);

        var old = await DeleteAsync(browser, new { password = Password, word = "DELETE" });
        var response = await DeleteAsync(browser, new { password = Password, word = "remove" });

        Assert.Equal("bad_word", await ErrorCodeAsync(old));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await CountAsync(app, $"SELECT count(*) FROM identity.users WHERE id = {userId}"));
    }

    [Fact]
    [Trait("Rule", "AUTH-36")]
    public async Task AUTH36_password_tries_are_limited()
    {
        await using var app = await StartAsync(("Identity:PasswordTries", "2"));
        var (browser, _) = await UserWithPasswordAsync(app);

        await DeleteAsync(browser, new { password = "wrong 1", word = "DELETE" });
        await DeleteAsync(browser, new { password = "wrong 2", word = "DELETE" });
        var response = await DeleteAsync(browser, new { password = Password, word = "DELETE" });

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("rate_limited", await ErrorCodeAsync(response));
    }
}
