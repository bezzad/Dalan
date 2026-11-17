using System.Net.Http.Json;
using Dalan.Testing;
using static Dalan.Identity.Tests.Http;

namespace Dalan.Identity.Tests;

public class SecurityLogTests(PostgresServer postgres)
{
    private const string Phone = "09121234567";

    [Fact]
    [Trait("Rule", "AUTH-17")]
    public async Task AUTH17_login_is_logged_with_the_user_id()
    {
        await using var app = await StartAsync(postgres);

        await app.LoginBySmsAsync(Phone);

        var userId = await PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT id FROM identity.users");
        Assert.Contains(app.Logs.Lines, line => line.Text.Contains($"Security event login: user {userId} by sms", StringComparison.Ordinal));
        Assert.Contains(app.Logs.Lines, line => line.Text.Contains("Security event sms_sent", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Rule", "AUTH-17")]
    public async Task AUTH17_failed_logins_and_refused_sms_are_logged()
    {
        await using var app = await StartAsync(postgres);
        var browser = app.Browser();

        await browser.PostAsJsonAsync("/api/auth/password", new { login = Phone, password = "wrong horse" }, Cancel);
        await browser.PostAsJsonAsync("/api/auth/sms/verify", new { phone = Phone, code = "000000" }, Cancel);
        await DalanApp.AskCodeAsync(browser, Phone);
        await DalanApp.AskCodeAsync(browser, Phone);

        Assert.Contains(app.Logs.Lines, line => line.Text.Contains("Security event login_failed: by pwd", StringComparison.Ordinal));
        Assert.Contains(app.Logs.Lines, line => line.Text.Contains("Security event login_failed: by sms", StringComparison.Ordinal));
        Assert.Contains(app.Logs.Lines, line => line.Text.Contains("Security event sms_refused", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Rule", "AUTH-17")]
    public async Task AUTH17_refresh_reuse_and_password_change_are_logged()
    {
        await using var app = await StartAsync(postgres);
        var (_, refresh) = await LoginRawAsync(app);
        await RefreshAsync(app, refresh);
        app.Time.Advance(TimeSpan.FromMinutes(1));
        await RefreshAsync(app, refresh);
        var (browser, _) = await app.LoginAsync();
        await browser.PostAsJsonAsync("/api/account/password", new { password = "correct horse" }, Cancel);

        Assert.Contains(app.Logs.Lines, line => line.Text.Contains("Security event refresh_reuse", StringComparison.Ordinal));
        Assert.Contains(app.Logs.Lines, line => line.Text.Contains("Security event password_set", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Rule", "AUTH-17")]
    public async Task AUTH17_no_code_password_token_or_whole_phone_number_in_the_log()
    {
        await using var app = await StartAsync(postgres);
        const string password = "correct horse battery";

        var browser = await app.LoginBySmsAsync(Phone);
        var setPassword = await browser.PostAsJsonAsync("/api/account/password", new { password }, Cancel);
        setPassword.EnsureSuccessStatusCode();
        var login = await NoJar(app).PostAsJsonAsync("/api/auth/password", new { login = Phone, password }, Cancel);
        await NoJar(app).PostAsJsonAsync("/api/auth/password", new { login = Phone, password = "a wrong password" }, Cancel);

        var log = string.Join('\n', app.Logs.Lines.Select(line => line.Text));
        Assert.Contains("Security event login", log, StringComparison.Ordinal);
        Assert.DoesNotContain(app.Sms.Sent[0].Code, log, StringComparison.Ordinal);
        Assert.DoesNotContain(password, log, StringComparison.Ordinal);
        Assert.DoesNotContain("a wrong password", log, StringComparison.Ordinal);
        Assert.DoesNotContain("9121234567", log, StringComparison.Ordinal);
        Assert.DoesNotContain(CookieValue(login, RefreshCookie), log, StringComparison.Ordinal);
        Assert.DoesNotContain(CookieValue(login, SessionCookie), log, StringComparison.Ordinal);
    }
}
