using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Testing;
using static Dalan.Identity.Tests.Http;

namespace Dalan.Identity.Tests;

/// <summary>AUTH-44 (trust this device for the second factor), and SEC-15 for an admin on a trusted device.</summary>
public class TrustedDeviceTests(PostgresServer postgres)
{
    private const string Phone = "09121234567";
    private const string Password = "correct horse";
    private const string AdminPhone = "09129990001";
    private const string TrustedCookie = "__Host-dalan_trusted";

    private Task<DalanApp> StartAsync(params (string Key, string Value)[] settings) =>
        Http.StartAsync(postgres, [("Identity:AdminPhones", AdminPhone), .. settings]);

    /// <summary>A user with a password and the second factor on. Returns the logged-in browser and the secret.</summary>
    private static async Task<(HttpClient Browser, byte[] Secret)> UserWithTotpAsync(DalanApp app, string phone = Phone)
    {
        var (browser, _) = await app.LoginAsync(phone: phone);
        (await browser.PostAsJsonAsync("/api/account/password", new { password = Password }, Cancel)).EnsureSuccessStatusCode();
        var (secret, _) = await app.TurnOnTotpAsync(browser);
        app.Time.Advance(TimeSpan.FromSeconds(30));
        return (browser, secret);
    }

    /// <summary>A password login from a browser that sends this trusted token (or none).</summary>
    private static async Task<HttpResponseMessage> PasswordLoginAsync(DalanApp app, string? trusted, string login = Phone)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/password")
        {
            Content = JsonContent.Create(new { login, password = Password }),
        };
        if (trusted is not null) request.Headers.Add("Cookie", $"{TrustedCookie}={trusted}");
        return await NoJar(app).SendAsync(request, Cancel);
    }

    private static async Task<string> TicketAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(SetCookie(response, SessionCookie));
        var body = await JsonAsync(response);
        Assert.True(body.GetProperty("totp_required").GetBoolean());
        return body.GetProperty("ticket").GetString()!;
    }

    private static async Task AssertSessionAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(SetCookie(response, SessionCookie));
        Assert.False((await JsonAsync(response)).TryGetProperty("totp_required", out _));
    }

    /// <summary>The code step with or without "trust"; returns the answer.</summary>
    private static Task<HttpResponseMessage> CodeAsync(DalanApp app, string ticket, string code, bool? trust) =>
        NoJar(app).PostAsJsonAsync("/api/auth/totp", new { ticket, code, trust }, Cancel);

    /// <summary>A password login and a right code with "trust": returns the trusted token this browser now keeps.</summary>
    private static async Task<string> TrustAsync(DalanApp app, byte[] secret, string login = Phone)
    {
        var response = await CodeAsync(app, await TicketAsync(await PasswordLoginAsync(app, null, login)), app.TotpCode(secret), trust: true);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        app.Time.Advance(TimeSpan.FromSeconds(30));
        return CookieValue(response, TrustedCookie);
    }

    private static Task<long> TrustedRowsAsync(DalanApp app) =>
        PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT count(*) FROM identity.trusted_devices");

    [Fact]
    [Trait("Rule", "AUTH-44")]
    public async Task AUTH44_a_right_code_with_trust_sets_the_cookie_and_the_next_login_skips_the_code()
    {
        await using var app = await StartAsync();
        var (_, secret) = await UserWithTotpAsync(app);

        var response = await CodeAsync(app, await TicketAsync(await PasswordLoginAsync(app, null)), app.TotpCode(secret), trust: true);

        await AssertSessionAsync(response);
        var cookie = SetCookie(response, TrustedCookie)!;
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max-age=2592000", cookie, StringComparison.OrdinalIgnoreCase);
        var token = CookieValue(response, TrustedCookie);
        Assert.Equal(1L, await TrustedRowsAsync(app));
        // Only the hash is kept.
        Assert.Equal(Sha256Hex(token), await PostgresServer.ScalarAsync<string>(app.ConnectionString,
            "SELECT encode(token_hash, 'hex') FROM identity.trusted_devices"));

        await AssertSessionAsync(await PasswordLoginAsync(app, token));
    }

    [Fact]
    [Trait("Rule", "AUTH-44")]
    public async Task AUTH44_without_trust_another_browser_or_another_user_the_code_is_asked()
    {
        await using var app = await StartAsync();
        var (_, secret) = await UserWithTotpAsync(app);
        var (_, otherSecret) = await UserWithTotpAsync(app, "09127654321");

        var plain = await CodeAsync(app, await TicketAsync(await PasswordLoginAsync(app, null)), app.TotpCode(secret), trust: null);
        Assert.Equal(HttpStatusCode.OK, plain.StatusCode);
        Assert.Null(SetCookie(plain, TrustedCookie));
        Assert.Equal(0L, await TrustedRowsAsync(app));
        app.Time.Advance(TimeSpan.FromSeconds(30));

        var othersToken = await TrustAsync(app, otherSecret, "09127654321");

        await TicketAsync(await PasswordLoginAsync(app, null));
        await TicketAsync(await PasswordLoginAsync(app, "made-up-token"));
        // The other user's token does not work for this user.
        await TicketAsync(await PasswordLoginAsync(app, othersToken));
    }

    [Fact]
    [Trait("Rule", "AUTH-44")]
    public async Task AUTH44_trust_ends_after_the_days()
    {
        await using var app = await StartAsync();
        var (_, secret) = await UserWithTotpAsync(app);
        var token = await TrustAsync(app, secret);

        app.Time.Advance(TimeSpan.FromDays(30));

        await TicketAsync(await PasswordLoginAsync(app, token));
    }

    [Fact]
    [Trait("Rule", "AUTH-44")]
    public async Task AUTH44_list_shows_the_current_one_and_forget_makes_the_code_come_back()
    {
        await using var app = await StartAsync();
        var (browser, secret) = await UserWithTotpAsync(app);
        var first = await TrustAsync(app, secret);
        var second = await TrustAsync(app, secret);

        // The browser that keeps the second token asks for the list.
        var session = await NoJar(app).PostAsJsonAsync("/api/dev/login", new { phone = Phone, name = "Sara" }, Cancel);
        using var list = new HttpRequestMessage(HttpMethod.Get, "/api/account/trusted-devices");
        list.Headers.Add("Cookie", $"{SessionCookie}={CookieValue(session, SessionCookie)}; {TrustedCookie}={second}");
        var devices = (await JsonAsync(await NoJar(app).SendAsync(list, Cancel))).GetProperty("devices").EnumerateArray().ToArray();

        Assert.Equal(2, devices.Length);
        Assert.Single(devices, d => d.GetProperty("current").GetBoolean());
        var current = devices.Single(d => d.GetProperty("current").GetBoolean());
        Assert.True(current.TryGetProperty("browser", out _));
        Assert.True(current.TryGetProperty("system", out _));
        Assert.True(current.TryGetProperty("expires_at", out _));
        var otherId = devices.Single(d => !d.GetProperty("current").GetBoolean()).GetProperty("id").GetInt64();

        Assert.Equal(HttpStatusCode.NoContent, (await browser.DeleteAsync($"/api/account/trusted-devices/{otherId}", Cancel)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.DeleteAsync($"/api/account/trusted-devices/{otherId}", Cancel)).StatusCode);

        await TicketAsync(await PasswordLoginAsync(app, first));
        await AssertSessionAsync(await PasswordLoginAsync(app, second));

        Assert.Equal(HttpStatusCode.NoContent, (await browser.DeleteAsync("/api/account/trusted-devices", Cancel)).StatusCode);
        Assert.Equal(0L, await TrustedRowsAsync(app));
        Assert.Equal(1L, await PostgresServer.ScalarAsync<long>(app.ConnectionString,
            "SELECT count(*) FROM admin.audit_log WHERE action = 'trusted_device.forget'"));
    }

    [Fact]
    [Trait("Rule", "AUTH-44")]
    public async Task AUTH44_setting_the_password_forgets_the_others_and_the_second_factor_forgets_all()
    {
        await using var app = await StartAsync();
        var (browser, secret) = await UserWithTotpAsync(app);
        var token = await TrustAsync(app, secret);

        (await browser.PostAsJsonAsync("/api/account/password", new { password = Password, current_password = Password }, Cancel))
            .EnsureSuccessStatusCode();
        await TicketAsync(await PasswordLoginAsync(app, token));

        var again = await TrustAsync(app, secret);
        var off = await browser.PostAsJsonAsync("/api/account/totp/off", new { code = app.TotpCode(secret) }, Cancel);
        Assert.Equal(HttpStatusCode.NoContent, off.StatusCode);
        Assert.Equal(0L, await TrustedRowsAsync(app));

        await app.TurnOnTotpAsync(browser);
        app.Time.Advance(TimeSpan.FromSeconds(30));
        await TicketAsync(await PasswordLoginAsync(app, again));
    }

    [Fact]
    [Trait("Rule", "AUTH-44")]
    public async Task AUTH44_zero_days_turns_trust_off()
    {
        await using var app = await StartAsync(("Identity:TrustedDeviceDays", "0"));
        var (_, secret) = await UserWithTotpAsync(app);

        var response = await CodeAsync(app, await TicketAsync(await PasswordLoginAsync(app, null)), app.TotpCode(secret), trust: true);

        await AssertSessionAsync(response);
        Assert.Null(SetCookie(response, TrustedCookie));
        Assert.Equal(0L, await TrustedRowsAsync(app));
    }

    [Fact]
    [Trait("Rule", "SEC-15")]
    [Trait("Rule", "AUTH-44")]
    public async Task SEC15_admin_on_a_trusted_device_skips_the_code_on_sms_login()
    {
        await using var app = await StartAsync();
        var admin = app.Browser();
        (await admin.PostAsJsonAsync("/api/dev/login", new { phone = AdminPhone, name = "Owner" }, Cancel)).EnsureSuccessStatusCode();
        var (secret, _) = await app.TurnOnTotpAsync(admin);
        app.Time.Advance(TimeSpan.FromSeconds(30));

        (await DalanApp.AskCodeAsync(NoJar(app), AdminPhone)).EnsureSuccessStatusCode();
        var first = await NoJar(app).PostAsJsonAsync("/api/auth/sms/verify", new { phone = AdminPhone, code = app.Sms.Sent[^1].Code }, Cancel);
        var trusted = await CodeAsync(app, await TicketAsync(first), app.TotpCode(secret), trust: true);
        var token = CookieValue(trusted, TrustedCookie);
        app.Time.Advance(TimeSpan.FromMinutes(5));

        (await DalanApp.AskCodeAsync(NoJar(app), AdminPhone)).EnsureSuccessStatusCode();
        using var verify = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sms/verify")
        {
            Content = JsonContent.Create(new { phone = AdminPhone, code = app.Sms.Sent[^1].Code }),
        };
        verify.Headers.Add("Cookie", $"{TrustedCookie}={token}");
        var second = await NoJar(app).SendAsync(verify, Cancel);

        await AssertSessionAsync(second);
        var settings = await SendAsync(app, HttpMethod.Get, "/api/admin/settings", session: CookieValue(second, SessionCookie));
        Assert.Equal(HttpStatusCode.OK, settings.StatusCode);
    }
}
