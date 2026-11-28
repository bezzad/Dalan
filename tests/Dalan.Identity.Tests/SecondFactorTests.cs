using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dalan.Identity.SecondFactor;
using Dalan.Relay.Bots;
using Dalan.Testing;
using Microsoft.Extensions.DependencyInjection;
using static Dalan.Identity.Tests.Http;

namespace Dalan.Identity.Tests;

/// <summary>AUTH-37 (second factor) and SEC-15 (second factor for admins).</summary>
public class SecondFactorTests(PostgresServer postgres)
{
    private const string Phone = "09121234567";
    private const string Password = "correct horse";
    private const string AdminPhone = "09129990001";

    private Task<DalanApp> StartAsync() => Http.StartAsync(postgres, ("Identity:AdminPhones", AdminPhone));

    /// <summary>A user with a password and the second factor on. Returns the browser, the secret and the recovery codes.</summary>
    private static async Task<(HttpClient Browser, byte[] Secret, string[] Codes)> UserWithTotpAsync(DalanApp app)
    {
        var (browser, _) = await app.LoginAsync(phone: Phone);
        (await browser.PostAsJsonAsync("/api/account/password", new { password = Password }, Cancel)).EnsureSuccessStatusCode();
        var (secret, codes) = await app.TurnOnTotpAsync(browser);
        // The code of this step was used to turn it on.
        app.Time.Advance(TimeSpan.FromSeconds(30));
        return (browser, secret, codes);
    }

    private static Task<HttpResponseMessage> PasswordLoginAsync(DalanApp app) =>
        NoJar(app).PostAsJsonAsync("/api/auth/password", new { login = Phone, password = Password }, Cancel);

    /// <summary>The ticket of a login answer that asks for the code; fails when the answer made a session.</summary>
    private static async Task<string> TicketAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(SetCookie(response, SessionCookie));
        Assert.Null(SetCookie(response, RefreshCookie));
        var body = await JsonAsync(response);
        Assert.True(body.GetProperty("totp_required").GetBoolean());
        Assert.False(body.TryGetProperty("id", out _));
        return body.GetProperty("ticket").GetString()!;
    }

    private static Task<HttpResponseMessage> CodeAsync(DalanApp app, string ticket, string code) =>
        NoJar(app).PostAsJsonAsync("/api/auth/totp", new { ticket, code }, Cancel);

    private static async Task<bool> TotpOnAsync(HttpClient browser) =>
        (await browser.GetFromJsonAsync<JsonElement>("/api/me", Cancel)).GetProperty("totp").GetBoolean();

    // ---- AUTH-37 ----

    [Theory]
    [Trait("Rule", "AUTH-37")]
    [InlineData(59, "287082")]
    [InlineData(1111111109, "081804")]
    [InlineData(1234567890, "005924")]
    [InlineData(2000000000, "279037")]
    public void AUTH37_codes_follow_rfc_6238(long unixSeconds, string expected)
    {
        var secret = Encoding.ASCII.GetBytes("12345678901234567890");
        Assert.Equal(expected, Totp.Code(secret, Totp.Step(DateTimeOffset.FromUnixTimeSeconds(unixSeconds), 30)));
        Assert.Equal("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", Totp.ToBase32(secret));
        Assert.Equal(secret, Totp.FromBase32("gezdgnbvgy3tqojqgezdgnbvgy3tqojq"));
    }

    [Fact]
    [Trait("Rule", "AUTH-37")]
    public async Task AUTH37_set_up_gives_secret_address_and_qr_and_is_not_on_yet()
    {
        await using var app = await StartAsync();
        var (browser, _) = await app.LoginAsync(phone: Phone);
        (await browser.PostAsJsonAsync("/api/account/username", new { username = "sara_rad" }, Cancel)).EnsureSuccessStatusCode();
        (await browser.PostAsJsonAsync("/api/account/password", new { password = Password }, Cancel)).EnsureSuccessStatusCode();

        var body = await JsonAsync(await browser.PostAsync("/api/account/totp", null, Cancel));

        var secret = body.GetProperty("secret").GetString()!;
        Assert.Equal(20, Totp.FromBase32(secret).Length);
        Assert.Equal($"otpauth://totp/Dalan:sara_rad?secret={secret}&issuer=Dalan", body.GetProperty("otpauth_url").GetString());
        Assert.StartsWith("<svg", body.GetProperty("qr_svg").GetString()!.TrimStart(), StringComparison.Ordinal);
        Assert.DoesNotContain("http", body.GetProperty("qr_svg").GetString()!.Replace("http://www.w3.org", "", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.False(await TotpOnAsync(browser));
        // Kept encrypted: the stored bytes are not the secret.
        var stored = await PostgresServer.ScalarAsync<byte[]>(app.ConnectionString, "SELECT secret FROM identity.totp");
        Assert.NotEqual(Totp.FromBase32(secret), stored);
    }

    [Fact]
    [Trait("Rule", "AUTH-37")]
    public async Task AUTH37_first_right_code_turns_it_on_and_gives_ten_recovery_codes_kept_hashed()
    {
        await using var app = await StartAsync();
        var (browser, _) = await app.LoginAsync(phone: Phone);
        (await browser.PostAsJsonAsync("/api/account/password", new { password = Password }, Cancel)).EnsureSuccessStatusCode();
        var secret = Totp.FromBase32((await JsonAsync(await browser.PostAsync("/api/account/totp", null, Cancel))).GetProperty("secret").GetString()!);

        var wrong = await browser.PostAsJsonAsync("/api/account/totp/confirm", new { code = app.TotpCode(secret, 5) }, Cancel);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal("wrong_code", await ErrorCodeAsync(wrong));
        Assert.False(await TotpOnAsync(browser));

        var right = await browser.PostAsJsonAsync("/api/account/totp/confirm", new { code = app.TotpCode(secret) }, Cancel);

        Assert.Equal(HttpStatusCode.OK, right.StatusCode);
        var codes = (await JsonAsync(right)).GetProperty("recovery_codes").EnumerateArray().Select(c => c.GetString()!).ToArray();
        Assert.Equal(10, codes.Distinct().Count());
        Assert.True(await TotpOnAsync(browser));
        Assert.Equal(10L, await PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT count(*) FROM identity.recovery_codes"));
        var hashes = await PostgresServer.ScalarAsync<string>(app.ConnectionString,
            "SELECT string_agg(encode(code_hash, 'escape'), ' ') FROM identity.recovery_codes");
        Assert.All(codes, code => Assert.DoesNotContain(code, hashes, StringComparison.Ordinal));
        Assert.Contains($"Security event totp_on: user", string.Join('\n', app.Logs.Lines.Select(l => l.Text)), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "AUTH-37")]
    public async Task AUTH37_user_without_password_who_is_not_admin_may_not_set_it_up()
    {
        await using var app = await StartAsync();
        var (browser, _) = await app.LoginAsync(phone: Phone);

        var response = await browser.PostAsync("/api/account/totp", null, Cancel);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("totp_not_allowed", await ErrorCodeAsync(response));
    }

    [Fact]
    [Trait("Rule", "AUTH-37")]
    public async Task AUTH37_password_login_asks_for_the_code_and_the_ticket_makes_the_session()
    {
        await using var app = await StartAsync();
        var (_, secret, _) = await UserWithTotpAsync(app);

        var ticket = await TicketAsync(await PasswordLoginAsync(app));
        var response = await CodeAsync(app, ticket, app.TotpCode(secret));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(SetCookie(response, SessionCookie));
        Assert.NotNull(SetCookie(response, RefreshCookie));
        var profile = await JsonAsync(response);
        Assert.True(profile.GetProperty("totp").GetBoolean());
        Assert.True(profile.GetProperty("has_password").GetBoolean());
        var me = await SendAsync(app, HttpMethod.Get, "/api/me", session: CookieValue(response, SessionCookie));
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        // The ticket is used once.
        Assert.Equal(HttpStatusCode.Unauthorized, (await CodeAsync(app, ticket, app.TotpCode(secret, 1))).StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-37")]
    public async Task AUTH37_codes_of_the_step_before_and_after_are_taken_and_a_used_code_is_refused()
    {
        await using var app = await StartAsync();
        var (_, secret, _) = await UserWithTotpAsync(app);
        // The step before must not be the one used to turn it on.
        app.Time.Advance(TimeSpan.FromSeconds(30));

        var before =await CodeAsync(app, await TicketAsync(await PasswordLoginAsync(app)), app.TotpCode(secret, -1));
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        var again = await CodeAsync(app, await TicketAsync(await PasswordLoginAsync(app)), app.TotpCode(secret, -1));
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal("wrong_code", await ErrorCodeAsync(again));

        var tooOld = await CodeAsync(app, await TicketAsync(await PasswordLoginAsync(app)), app.TotpCode(secret, -2));
        Assert.Equal(HttpStatusCode.BadRequest, tooOld.StatusCode);

        var after = await CodeAsync(app, await TicketAsync(await PasswordLoginAsync(app)), app.TotpCode(secret, 1));
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-37")]
    public async Task AUTH37_recovery_code_works_once()
    {
        await using var app = await StartAsync();
        var (_, _, codes) = await UserWithTotpAsync(app);

        var first = await CodeAsync(app, await TicketAsync(await PasswordLoginAsync(app)), codes[3].ToLowerInvariant());
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(9L, await PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT count(*) FROM identity.recovery_codes"));

        var second = await CodeAsync(app, await TicketAsync(await PasswordLoginAsync(app)), codes[3]);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal("wrong_code", await ErrorCodeAsync(second));
    }

    [Fact]
    [Trait("Rule", "AUTH-37")]
    public async Task AUTH37_old_ticket_is_401()
    {
        await using var app = await StartAsync();
        var (_, secret, _) = await UserWithTotpAsync(app);
        var ticket = await TicketAsync(await PasswordLoginAsync(app));

        app.Time.Advance(TimeSpan.FromMinutes(5));
        var response = await CodeAsync(app, ticket, app.TotpCode(secret));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthorized", await ErrorCodeAsync(response));
    }

    [Fact]
    [Trait("Rule", "AUTH-37")]
    public async Task AUTH37_fifth_wrong_code_ends_the_ticket()
    {
        await using var app = await StartAsync();
        var (_, secret, _) = await UserWithTotpAsync(app);
        var ticket = await TicketAsync(await PasswordLoginAsync(app));

        for (var i = 0; i < 4; i++)
            Assert.Equal(HttpStatusCode.BadRequest, (await CodeAsync(app, ticket, "000000")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await CodeAsync(app, ticket, "000000")).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await CodeAsync(app, ticket, app.TotpCode(secret))).StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-37")]
    public async Task AUTH37_turn_off_needs_a_right_code_and_removes_the_recovery_codes()
    {
        await using var app = await StartAsync();
        var (browser, secret, _) = await UserWithTotpAsync(app);

        var wrong = await browser.PostAsJsonAsync("/api/account/totp/off", new { code = "000000" }, Cancel);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal("wrong_code", await ErrorCodeAsync(wrong));
        Assert.True(await TotpOnAsync(browser));

        var off = await browser.PostAsJsonAsync("/api/account/totp/off", new { code = app.TotpCode(secret) }, Cancel);

        Assert.Equal(HttpStatusCode.NoContent, off.StatusCode);
        Assert.False(await TotpOnAsync(browser));
        Assert.Equal(0L, await PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT count(*) FROM identity.recovery_codes"));
        var login = await PasswordLoginAsync(app);
        Assert.NotNull(SetCookie(login, SessionCookie));
    }

    [Fact]
    [Trait("Rule", "AUTH-37")]
    public async Task AUTH37_guessing_the_code_to_turn_it_off_is_limited_like_a_password()
    {
        await using var app = await StartAsync();
        var (browser, secret, _) = await UserWithTotpAsync(app);

        // Identity:PasswordTries (5) wrong codes, then even the right code waits.
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsJsonAsync("/api/account/totp/off", new { code = "000000" }, Cancel)).StatusCode);
        var limited = await browser.PostAsJsonAsync("/api/account/totp/off", new { code = app.TotpCode(secret) }, Cancel);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("rate_limited", await ErrorCodeAsync(limited));
        Assert.True(await TotpOnAsync(browser));
    }

    [Fact]
    [Trait("Rule", "AUTH-37")]
    public async Task AUTH37_sms_and_email_logins_of_a_common_user_do_not_ask()
    {
        await using var app = await StartAsync();
        var (browser, _, _) = await UserWithTotpAsync(app);
        await PostgresServer.ExecuteAsync(app.ConnectionString, "UPDATE identity.users SET email = 'sara@example.com'");

        var bySms = await app.LoginBySmsAsync(Phone);
        var byEmail = await app.LoginByEmailAsync("sara@example.com");

        Assert.True(await TotpOnAsync(bySms));
        Assert.True(await TotpOnAsync(byEmail));
    }

    // ---- SEC-15 ----

    /// <summary>The admin's browser by the development login, without the second factor (the test helper would turn it on).</summary>
    private static async Task<HttpClient> AdminWithoutTotpAsync(DalanApp app)
    {
        var browser = app.Browser();
        (await browser.PostAsJsonAsync("/api/dev/login", new { phone = AdminPhone, name = "Owner" }, Cancel)).EnsureSuccessStatusCode();
        return browser;
    }

    [Fact]
    [Trait("Rule", "SEC-15")]
    public async Task SEC15_admin_without_second_factor_gets_admin_needs_2fa_until_it_is_on()
    {
        await using var app = await StartAsync();
        var admin = await AdminWithoutTotpAsync(app);

        var refused = await admin.GetAsync("/api/admin/settings", Cancel);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("admin_needs_2fa", await ErrorCodeAsync(refused));
        var me = await admin.GetFromJsonAsync<JsonElement>("/api/me", Cancel);
        Assert.True(me.GetProperty("admin").GetBoolean());
        Assert.False(me.GetProperty("totp").GetBoolean());

        // An admin may set it up without a password.
        await app.TurnOnTotpAsync(admin);

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/admin/settings", Cancel)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "SEC-15")]
    public async Task SEC15_common_user_still_gets_not_admin()
    {
        await using var app = await StartAsync();
        var (browser, _) = await app.LoginAsync(phone: Phone);

        var response = await browser.GetAsync("/api/admin/settings", Cancel);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("not_admin", await ErrorCodeAsync(response));
    }

    [Fact]
    [Trait("Rule", "SEC-15")]
    public async Task SEC15_admin_sms_login_asks_for_the_code()
    {
        await using var app = await StartAsync();
        var (secret, _) = await app.TurnOnTotpAsync(await AdminWithoutTotpAsync(app));
        app.Time.Advance(TimeSpan.FromSeconds(30));

        (await DalanApp.AskCodeAsync(NoJar(app), AdminPhone)).EnsureSuccessStatusCode();
        var answer = await NoJar(app).PostAsJsonAsync("/api/auth/sms/verify", new { phone = AdminPhone, code = app.Sms.Sent[^1].Code }, Cancel);
        var ticket = await TicketAsync(answer);

        var response = await CodeAsync(app, ticket, app.TotpCode(secret));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var admin = await SendAsync(app, HttpMethod.Get, "/api/admin/settings", session: CookieValue(response, SessionCookie));
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
    }

    [Fact]
    [Trait("Rule", "SEC-15")]
    public async Task SEC15_admin_email_and_password_logins_ask_for_the_code()
    {
        await using var app = await StartAsync();
        var browser = await AdminWithoutTotpAsync(app);
        (await browser.PostAsJsonAsync("/api/account/password", new { password = Password }, Cancel)).EnsureSuccessStatusCode();
        await app.TurnOnTotpAsync(browser);
        await PostgresServer.ExecuteAsync(app.ConnectionString, "UPDATE identity.users SET email = 'owner@example.com'");

        (await DalanApp.AskEmailCodeAsync(NoJar(app), "owner@example.com")).EnsureSuccessStatusCode();
        await TicketAsync(await NoJar(app).PostAsJsonAsync(
            "/api/auth/email/verify", new { email = "owner@example.com", code = app.Email.Sent[^1].Code }, Cancel));
        await TicketAsync(await NoJar(app).PostAsJsonAsync("/api/auth/password", new { login = AdminPhone, password = Password }, Cancel));
    }

    [Fact]
    [Trait("Rule", "SEC-15")]
    public async Task SEC15_turning_it_on_ends_every_other_session()
    {
        await using var app = await StartAsync();
        // A session made before the second factor, on another device (a stolen SMS session, say).
        var (_, stolenRefresh) = await LoginRawAsync(app, AdminPhone);
        var (browser, _) = await app.LoginAsync(phone: AdminPhone);

        Assert.True(await TotpOnAsync(browser));
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(app, stolenRefresh)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/admin/settings", Cancel)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "SEC-15")]
    public async Task SEC15_admin_cannot_turn_it_off()
    {
        await using var app = await StartAsync();
        var admin = await AdminWithoutTotpAsync(app);
        var (secret, _) = await app.TurnOnTotpAsync(admin);
        app.Time.Advance(TimeSpan.FromSeconds(30));

        var response = await admin.PostAsJsonAsync("/api/account/totp/off", new { code = app.TotpCode(secret) }, Cancel);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("admin_needs_2fa", await ErrorCodeAsync(response));
        Assert.True(await TotpOnAsync(admin));
    }


    /// <summary>The admin (second factor on) and another account that proved the admin's phone: a pending merge.</summary>
    private static async Task<(HttpClient Other, long AdminId, long OtherId, byte[] Secret, string[] Codes)> AdminMergeAsync(DalanApp app)
    {
        var admin = await AdminWithoutTotpAsync(app);
        var adminId = (await admin.GetFromJsonAsync<JsonElement>("/api/me", Cancel)).GetProperty("id").GetInt64();
        var (secret, codes) = await app.TurnOnTotpAsync(admin);
        app.Time.Advance(TimeSpan.FromSeconds(30));
        var other = await app.LoginByEmailAsync("owner@example.com");
        var otherId = (await other.GetFromJsonAsync<JsonElement>("/api/me", Cancel)).GetProperty("id").GetInt64();
        (await DalanApp.AskCodeAsync(other, AdminPhone)).EnsureSuccessStatusCode();
        var proof = await other.PostAsJsonAsync("/api/account/phone", new { phone = AdminPhone, code = app.Sms.Sent[^1].Code }, Cancel);
        Assert.Equal(HttpStatusCode.Conflict, proof.StatusCode);
        return (other, adminId, otherId, secret, codes);
    }

    private static Task<HttpResponseMessage> MergeAsync(HttpClient browser, string? code) =>
        browser.PostAsJsonAsync("/api/account/merge", new { code }, Cancel);

    private static Task<long> UsersAsync(DalanApp app) => PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT count(*) FROM identity.users");

    [Fact]
    [Trait("Rule", "SEC-15")]
    [Trait("Rule", "AUTH-32")]
    public async Task SEC15_a_merge_with_an_admin_needs_the_admin_code_and_keeps_the_admin()
    {
        await using var app = await StartAsync();
        var (other, adminId, otherId, secret, _) = await AdminMergeAsync(app);

        var plan = await other.GetFromJsonAsync<JsonElement>("/api/account/merge", Cancel);
        Assert.True(plan.GetProperty("needs_code").GetBoolean());
        Assert.Equal("other", plan.GetProperty("keep").GetString());
        Assert.Equal(JsonValueKind.Null, plan.GetProperty("refusal").ValueKind);

        var none = await other.PostAsync("/api/account/merge", null, Cancel);
        var wrong = await MergeAsync(other, app.TotpCode(secret, 5));
        Assert.Equal((HttpStatusCode.Forbidden, "admin_merge"), (none.StatusCode, await ErrorCodeAsync(none)));
        Assert.Equal((HttpStatusCode.Forbidden, "admin_merge"), (wrong.StatusCode, await ErrorCodeAsync(wrong)));
        Assert.Equal(2, await UsersAsync(app));
        Assert.False((await other.GetFromJsonAsync<JsonElement>("/api/me", Cancel)).GetProperty("admin").GetBoolean());

        var right = await MergeAsync(other, app.TotpCode(secret));

        Assert.Equal(HttpStatusCode.OK, right.StatusCode);
        var me = await other.GetFromJsonAsync<JsonElement>("/api/me", Cancel);
        Assert.Equal(adminId, me.GetProperty("id").GetInt64());
        Assert.True(me.GetProperty("admin").GetBoolean());
        Assert.Equal("owner@example.com", me.GetProperty("email").GetString());
        Assert.Equal(1, await UsersAsync(app));
        Assert.Equal(0, await PostgresServer.ScalarAsync<long>(app.ConnectionString, $"SELECT count(*) FROM identity.users WHERE id = {otherId}"));
        Assert.Equal(1, await PostgresServer.ScalarAsync<long>(app.ConnectionString,
            $"SELECT count(*) FROM admin.audit_log WHERE action = 'account.merge' AND detail = 'user {otherId} merged into user {adminId} (phone)'"));
    }

    [Fact]
    [Trait("Rule", "AUTH-32")]
    public async Task AUTH32_an_admin_is_not_kept_over_the_other_accounts_products()
    {
        await using var app = await StartAsync();
        var (other, _, otherId, secret, _) = await AdminMergeAsync(app);
        await app.Services.GetRequiredService<BotRegistry>().CreateAsync(otherId, "email_bot", "Bot", Cancel);

        var plan = await other.GetFromJsonAsync<JsonElement>("/api/account/merge", Cancel);
        var response = await MergeAsync(other, app.TotpCode(secret));

        // The admin's account would be kept and the other deleted with its bot: a merge never deletes a product.
        Assert.Equal("other_has_products", plan.GetProperty("refusal").GetString());
        Assert.Equal((HttpStatusCode.Conflict, "other_has_products"), (response.StatusCode, await ErrorCodeAsync(response)));
        Assert.Equal(2, await UsersAsync(app));
        Assert.Equal(1, await PostgresServer.ScalarAsync<long>(app.ConnectionString,
            $"SELECT count(*) FROM relay.bots WHERE owner_user_id = {otherId} AND deleted_at IS NULL"));
    }

    [Fact]
    [Trait("Rule", "SEC-15")]
    public async Task SEC15_a_recovery_code_merges_and_is_spent()
    {
        await using var app = await StartAsync();
        var (other, adminId, _, _, codes) = await AdminMergeAsync(app);

        var response = await MergeAsync(other, codes[0]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(adminId, (await JsonAsync(response)).GetProperty("id").GetInt64());
        Assert.Equal(codes.Length - 1, await PostgresServer.ScalarAsync<long>(app.ConnectionString,
            $"SELECT count(*) FROM identity.recovery_codes WHERE user_id = {adminId}"));
    }

    [Fact]
    [Trait("Rule", "SEC-15")]
    public async Task SEC15_guessing_the_merge_code_is_limited_like_a_password()
    {
        await using var app = await StartAsync();
        var (other, _, _, secret, _) = await AdminMergeAsync(app);

        // Identity:PasswordTries (5) wrong codes, then even the right code waits.
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Forbidden, (await MergeAsync(other, "000000")).StatusCode);
        var limited = await MergeAsync(other, app.TotpCode(secret));

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("rate_limited", await ErrorCodeAsync(limited));
        Assert.Equal(2, await UsersAsync(app));
    }

    [Fact]
    [Trait("Rule", "SEC-15")]
    public async Task SEC15_two_admin_accounts_are_never_merged()
    {
        const string SecondAdmin = "09129990002";
        await using var app = await Http.StartAsync(postgres, ("Identity:AdminPhones", $"{AdminPhone}, {SecondAdmin}"));
        await app.AddUserAsync("Second admin", phone: "+989129990002");
        var admin = await AdminWithoutTotpAsync(app);
        var (secret, _) = await app.TurnOnTotpAsync(admin);
        app.Time.Advance(TimeSpan.FromSeconds(30));
        await PostgresServer.ExecuteAsync(app.ConnectionString, $"UPDATE identity.users SET email = 'second@example.com' WHERE phone = '+989129990002'");
        (await DalanApp.AskEmailCodeAsync(admin, "second@example.com")).EnsureSuccessStatusCode();
        var proof = await admin.PostAsJsonAsync("/api/account/email", new { email = "second@example.com", code = app.Email.Sent[^1].Code }, Cancel);
        Assert.Equal(HttpStatusCode.Conflict, proof.StatusCode);

        var response = await MergeAsync(admin, app.TotpCode(secret));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("admin_merge", await ErrorCodeAsync(response));
        Assert.Equal(2, await UsersAsync(app));
    }
}
