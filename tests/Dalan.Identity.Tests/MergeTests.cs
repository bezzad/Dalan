using System.Net;
using System.Net.Http.Json;
using Dalan.Relay.Bots;
using Dalan.Testing;
using Microsoft.Extensions.DependencyInjection;
using static Dalan.Identity.Tests.Http;

namespace Dalan.Identity.Tests;

/// <summary>Link a login way and merge two accounts of one person (AUTH-26, AUTH-30 to AUTH-34).</summary>
public class MergeTests(PostgresServer postgres)
{
    private const string Email = "sara@example.com";
    private const string Phone = "09121234567";
    private const string PhoneE164 = "+989121234567";

    /// <summary>The browser proves a phone with the right SMS code (AUTH-26).</summary>
    private static async Task<HttpResponseMessage> ProvePhoneAsync(DalanApp app, HttpClient browser, string phone = Phone)
    {
        (await DalanApp.AskCodeAsync(browser, phone)).EnsureSuccessStatusCode();
        return await browser.PostAsJsonAsync("/api/account/phone", new { phone, code = app.Sms.Sent[^1].Code }, Cancel);
    }

    /// <summary>The browser proves an email with the right email code (AUTH-30).</summary>
    private static async Task<HttpResponseMessage> ProveEmailAsync(DalanApp app, HttpClient browser, string email)
    {
        (await DalanApp.AskEmailCodeAsync(browser, email)).EnsureSuccessStatusCode();
        return await browser.PostAsJsonAsync("/api/account/email", new { email, code = app.Email.Sent[^1].Code }, Cancel);
    }

    private static async Task<long> IdAsync(HttpClient browser) =>
        (await JsonAsync(await browser.GetAsync("/api/me", Cancel))).GetProperty("id").GetInt64();

    private static async Task AddBotAsync(DalanApp app, long ownerUserId, string username) =>
        await app.Services.GetRequiredService<BotRegistry>().CreateAsync(ownerUserId, username, "Bot", Cancel);

    private static Task<long> CountAsync(DalanApp app, string sql) => PostgresServer.ScalarAsync<long>(app.ConnectionString, sql);

    /// <summary>An email user (the current session) and a phone account of the same person.</summary>
    private async Task<(DalanApp App, HttpClient Browser, long Me, long Other)> TwoAccountsAsync()
    {
        var app = await StartAsync(postgres);
        var other = await app.AddUserAsync("Sara phone", phone: PhoneE164);
        var browser = await app.LoginByEmailAsync(Email);
        return (app, browser, await IdAsync(browser), other);
    }

    // ---- AUTH-26 ----

    [Fact]
    [Trait("Rule", "AUTH-26")]
    public async Task AUTH26_phone_of_another_user_starts_a_merge_and_changes_nothing()
    {
        var (app, browser, _, _) = await TwoAccountsAsync();
        await using var _app = app;

        var response = await ProvePhoneAsync(app, browser);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("merge_needed", await ErrorCodeAsync(response));
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/account/merge", Cancel)).StatusCode);
        Assert.False((await JsonAsync(await browser.GetAsync("/api/me", Cancel))).GetProperty("has_phone").GetBoolean());
        Assert.Equal(2, await CountAsync(app, "SELECT count(*) FROM identity.users"));
    }

    // ---- AUTH-30 ----

    [Fact]
    [Trait("Rule", "AUTH-30")]
    public async Task AUTH30_link_a_free_email_then_log_in_by_it()
    {
        await using var app = await StartAsync(postgres);
        var browser = await app.LoginBySmsAsync(Phone);
        var me = await IdAsync(browser);

        var response = await ProveEmailAsync(app, browser, Email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Email, (await JsonAsync(response)).GetProperty("email").GetString());
        app.Time.Advance(TimeSpan.FromSeconds(300));
        Assert.Equal(me, await IdAsync(await app.LoginByEmailAsync(Email)));
    }

    [Fact]
    [Trait("Rule", "AUTH-30")]
    public async Task AUTH30_email_of_another_account_starts_a_merge()
    {
        await using var app = await StartAsync(postgres);
        await app.LoginByEmailAsync(Email);
        app.Time.Advance(TimeSpan.FromSeconds(300));
        var browser = await app.LoginBySmsAsync(Phone);

        var response = await ProveEmailAsync(app, browser, Email);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("merge_needed", await ErrorCodeAsync(response));
        Assert.Equal("email", (await JsonAsync(await browser.GetAsync("/api/account/merge", Cancel))).GetProperty("way").GetString());
    }

    [Fact]
    [Trait("Rule", "AUTH-30")]
    public async Task AUTH30_already_has_an_email()
    {
        await using var app = await StartAsync(postgres);
        var browser = await app.LoginByEmailAsync(Email);

        var response = await browser.PostAsJsonAsync("/api/account/email", new { email = "other@example.com", code = "000000" }, Cancel);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("has_email", await ErrorCodeAsync(response));
    }

    // ---- AUTH-31 ----

    [Fact]
    [Trait("Rule", "AUTH-31")]
    public async Task AUTH31_no_proof_no_answer()
    {
        var (app, browser, _, _) = await TwoAccountsAsync();
        await using var _app = app;
        (await DalanApp.AskCodeAsync(browser, Phone)).EnsureSuccessStatusCode();
        var wrong = app.Sms.Sent[^1].Code == "000000" ? "000001" : "000000";

        var response = await browser.PostAsJsonAsync("/api/account/phone", new { phone = Phone, code = wrong }, Cancel);

        Assert.Equal("wrong_code", await ErrorCodeAsync(response));
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync("/api/account/merge", Cancel)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-31")]
    public async Task AUTH31_the_question_shows_the_way_the_kept_account_and_the_counts()
    {
        var (app, browser, me, other) = await TwoAccountsAsync();
        await using var _app = app;
        await AddBotAsync(app, other, "first_bot");
        await AddBotAsync(app, other, "second_bot");
        await ProvePhoneAsync(app, browser);

        var plan = await JsonAsync(await browser.GetAsync("/api/account/merge", Cancel));

        Assert.Equal("phone", plan.GetProperty("way").GetString());
        Assert.Equal("other", plan.GetProperty("keep").GetString());
        Assert.Equal(me, plan.GetProperty("this").GetProperty("id").GetInt64());
        Assert.Equal(2, plan.GetProperty("other").GetProperty("bots").GetInt32());
        Assert.Equal(0, plan.GetProperty("this").GetProperty("bots").GetInt32());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, plan.GetProperty("refusal").ValueKind);
    }

    [Fact]
    [Trait("Rule", "AUTH-31")]
    public async Task AUTH31_a_pending_merge_ends()
    {
        var (app, browser, _, _) = await TwoAccountsAsync();
        await using var _app = app;
        await ProvePhoneAsync(app, browser);
        app.Time.Advance(TimeSpan.FromSeconds(601));

        var response = await browser.PostAsync("/api/account/merge", null, Cancel);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(2, await CountAsync(app, "SELECT count(*) FROM identity.users"));
    }

    [Fact]
    [Trait("Rule", "AUTH-31")]
    public async Task AUTH31_cancel_and_a_new_proof_replaces_it()
    {
        var (app, browser, _, _) = await TwoAccountsAsync();
        await using var _app = app;
        var third = await app.AddUserAsync("Third", phone: "+989127654321");
        await ProvePhoneAsync(app, browser);
        app.Time.Advance(TimeSpan.FromSeconds(300));

        await ProvePhoneAsync(app, browser, "09127654321");
        var plan = await JsonAsync(await browser.GetAsync("/api/account/merge", Cancel));
        var cancel = await browser.DeleteAsync("/api/account/merge", Cancel);

        Assert.Equal(third, plan.GetProperty("other").GetProperty("id").GetInt64());
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync("/api/account/merge", Cancel)).StatusCode);
        Assert.Equal(3, await CountAsync(app, "SELECT count(*) FROM identity.users"));
    }

    // ---- AUTH-32 ----

    [Fact]
    [Trait("Rule", "AUTH-32")]
    public async Task AUTH32_the_account_with_bots_is_kept_and_its_bots_do_not_change()
    {
        var (app, browser, me, other) = await TwoAccountsAsync();
        await using var _app = app;
        await AddBotAsync(app, other, "first_bot");
        await AddBotAsync(app, other, "second_bot");
        await ProvePhoneAsync(app, browser);

        var response = await browser.PostAsync("/api/account/merge", null, Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(other, (await JsonAsync(response)).GetProperty("id").GetInt64());
        Assert.Equal(2, await CountAsync(app, $"SELECT count(*) FROM relay.bots WHERE owner_user_id = {other}"));
        var now = await JsonAsync(await browser.GetAsync("/api/me", Cancel));
        Assert.Equal(other, now.GetProperty("id").GetInt64());
        Assert.Equal(Email, now.GetProperty("email").GetString());
        Assert.Equal(0, await CountAsync(app, $"SELECT count(*) FROM identity.users WHERE id = {me}"));
    }

    [Fact]
    [Trait("Rule", "AUTH-37")]
    public async Task AUTH37_a_merge_moves_the_second_factor_past_a_set_up_never_confirmed()
    {
        var (app, browser, me, other) = await TwoAccountsAsync();
        await using var _app = app;
        // The kept account (this one) started a set-up and never confirmed it; the other one has the second factor on.
        await PostgresServer.ExecuteAsync(app.ConnectionString, $"""
            INSERT INTO identity.totp (user_id, secret) VALUES ({me}, '\x00'::bytea);
            INSERT INTO identity.totp (user_id, secret, on_at) VALUES ({other}, '\x01'::bytea, now());
            INSERT INTO identity.recovery_codes (user_id, code_hash) VALUES ({other}, '\x02'::bytea);
            """);
        await ProvePhoneAsync(app, browser);

        (await browser.PostAsync("/api/account/merge", null, Cancel)).EnsureSuccessStatusCode();

        Assert.Equal(1, await CountAsync(app, $"SELECT count(*) FROM identity.totp WHERE user_id = {me} AND on_at IS NOT NULL"));
        Assert.Equal(1, await CountAsync(app, $"SELECT count(*) FROM identity.recovery_codes WHERE user_id = {me}"));
    }

    [Fact]
    [Trait("Rule", "AUTH-32")]
    public async Task AUTH32_with_no_products_the_current_account_is_kept()
    {
        var (app, browser, me, _) = await TwoAccountsAsync();
        await using var _app = app;
        await ProvePhoneAsync(app, browser);

        var response = await browser.PostAsync("/api/account/merge", null, Cancel);

        var profile = await JsonAsync(response);
        Assert.Equal(me, profile.GetProperty("id").GetInt64());
        Assert.True(profile.GetProperty("has_phone").GetBoolean());
        Assert.Equal(1, await CountAsync(app, "SELECT count(*) FROM identity.users"));
    }

    [Fact]
    [Trait("Rule", "AUTH-32")]
    public async Task AUTH32_both_with_products_is_refused()
    {
        var (app, browser, me, other) = await TwoAccountsAsync();
        await using var _app = app;
        await AddBotAsync(app, me, "my_bot");
        await AddBotAsync(app, other, "their_bot");
        await ProvePhoneAsync(app, browser);

        var response = await browser.PostAsync("/api/account/merge", null, Cancel);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("both_have_products", await ErrorCodeAsync(response));
        Assert.Equal(2, await CountAsync(app, "SELECT count(*) FROM identity.users"));
    }

    // ---- AUTH-33 ----

    [Fact]
    [Trait("Rule", "AUTH-33")]
    public async Task AUTH33_the_kept_account_takes_only_empty_fields()
    {
        var (app, browser, _, other) = await TwoAccountsAsync();
        await using var _app = app;
        (await browser.PostAsJsonAsync("/api/account/password", new { password = "a long password 1" }, Cancel)).EnsureSuccessStatusCode();
        await AddBotAsync(app, other, "their_bot");
        await ProvePhoneAsync(app, browser);

        var profile = await JsonAsync(await browser.PostAsync("/api/account/merge", null, Cancel));

        Assert.Equal(other, profile.GetProperty("id").GetInt64());
        Assert.Equal("Sara phone", profile.GetProperty("name").GetString());
        Assert.True(profile.GetProperty("has_phone").GetBoolean());
        Assert.Equal(Email, profile.GetProperty("email").GetString());
        Assert.True(profile.GetProperty("has_password").GetBoolean());
    }

    [Fact]
    [Trait("Rule", "AUTH-33")]
    public async Task AUTH33_two_different_emails_are_refused()
    {
        var (app, browser, _, other) = await TwoAccountsAsync();
        await using var _app = app;
        await PostgresServer.ExecuteAsync(app.ConnectionString, $"UPDATE identity.users SET email = 'other@example.com' WHERE id = {other}");
        await ProvePhoneAsync(app, browser);

        var plan = await JsonAsync(await browser.GetAsync("/api/account/merge", Cancel));
        var response = await browser.PostAsync("/api/account/merge", null, Cancel);

        Assert.Equal("merge_conflict", plan.GetProperty("refusal").GetString());
        Assert.Equal("email", plan.GetProperty("field").GetString());
        Assert.Equal("merge_conflict", await ErrorCodeAsync(response));
        Assert.Equal(2, await CountAsync(app, "SELECT count(*) FROM identity.users"));
    }

    // ---- AUTH-34 ----

    [Fact]
    [Trait("Rule", "AUTH-34")]
    public async Task AUTH34_logged_in_as_the_kept_account_and_the_old_one_is_gone()
    {
        var (app, browser, me, other) = await TwoAccountsAsync();
        await using var _app = app;
        await AddBotAsync(app, other, "their_bot");
        await ProvePhoneAsync(app, browser);

        var response = await browser.PostAsync("/api/account/merge", null, Cancel);

        Assert.NotNull(SetCookie(response, SessionCookie));
        Assert.Equal(other, await IdAsync(browser));
        Assert.Equal(0, await CountAsync(app, $"SELECT count(*) FROM identity.sessions WHERE user_id = {me}"));
    }

    [Fact]
    [Trait("Rule", "AUTH-34")]
    public async Task AUTH34_audit_log_has_the_merge_and_the_access_log_is_unchanged()
    {
        var (app, browser, me, other) = await TwoAccountsAsync();
        await using var _app = app;
        await AddBotAsync(app, other, "their_bot");
        await ProvePhoneAsync(app, browser);
        var accessRows = await CountAsync(app, $"SELECT count(*) FROM admin.access_log WHERE actor_id = {me}");

        (await browser.PostAsync("/api/account/merge", null, Cancel)).EnsureSuccessStatusCode();

        var detail = await PostgresServer.ScalarAsync<string>(app.ConnectionString,
            "SELECT detail FROM admin.audit_log WHERE action = 'account.merge'");
        Assert.Contains($"user {me} merged into user {other}", detail, StringComparison.Ordinal);
        Assert.Equal(accessRows, await CountAsync(app, $"SELECT count(*) FROM admin.access_log WHERE actor_id = {me}"));
        Assert.Equal(1, await CountAsync(app, "SELECT count(*) FROM identity.merges WHERE done_at IS NOT NULL"));
    }
}
