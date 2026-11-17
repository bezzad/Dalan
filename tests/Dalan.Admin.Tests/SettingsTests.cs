using System.Net;
using System.Net.Http.Json;
using Dalan.Admin.AccessLog;
using Dalan.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Admin.Tests;

public class SettingsTests(PostgresServer postgres)
{
    [Fact]
    [Trait("Rule", "ADM-06")]
    public async Task ADM06_limits_are_listed_with_kind()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();

        var retention = await admin.SettingAsync("Admin:AccessLogRetentionDays");
        Assert.Equal("180", retention.GetProperty("value").GetString());
        Assert.Equal("180", retention.GetProperty("start").GetString());
        Assert.False(retention.GetProperty("changed").GetBoolean());
        Assert.Equal("number", retention.GetProperty("kind").GetString());
        Assert.Equal("number", (await admin.SettingAsync("Relay:BotOnlineSeconds")).GetProperty("kind").GetString());
        Assert.Equal("number", (await admin.SettingAsync("Relay:ClientStatusRefreshSeconds")).GetProperty("kind").GetString());
        Assert.Equal("switch", (await admin.SettingAsync("Relay:LatestCommandWins")).GetProperty("kind").GetString());
        Assert.Equal("text", (await admin.SettingAsync("Notification:Push:AllowedHosts")).GetProperty("kind").GetString());
        Assert.Equal("number", (await admin.SettingAsync("Dalan:IpRequestLimit")).GetProperty("kind").GetString());
    }

    [Fact]
    [Trait("Rule", "ADM-06")]
    public async Task ADM06_secrets_are_never_listed()
    {
        await using var app = await AdminApp.StartAsync(postgres, ("Notification:Kavenegar:ApiKey", "kavenegar-secret-value"));
        var (admin, _) = await app.AdminAsync();

        var response = await admin.GetAsync("/api/admin/settings", AdminApp.Cancel);
        var text = await response.Content.ReadAsStringAsync(AdminApp.Cancel);
        var keys = (await response.JsonAsync()).GetProperty("settings").EnumerateArray().Select(s => s.GetProperty("key").GetString()).ToList();

        foreach (var hidden in new[]
        {
            "Identity:SigningKey", "Identity:DevLogin", "Identity:AdminPhones", "Notification:SmsProvider",
            "Notification:Kavenegar:ApiKey", "Notification:Kavenegar:BaseUrl", "Notification:Push:PublicKey",
            "Notification:Push:PrivateKey", "Relay:FileFolder", "Dalan:TrustedProxies", "Dalan:Version",
            "ConnectionStrings:Dalan",
        })
        {
            Assert.DoesNotContain(hidden, keys);
        }

        Assert.DoesNotContain("kavenegar-secret-value", text, StringComparison.Ordinal);
        Assert.DoesNotContain(AdminApp.AdminPhone, text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "ADM-06")]
    public async Task ADM06_start_only_setting_is_marked()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();

        Assert.True((await admin.SettingAsync("Notification:Push:Workers")).GetProperty("restart").GetBoolean());
        Assert.False((await admin.SettingAsync("Relay:MaxTextLength")).GetProperty("restart").GetBoolean());
    }

    [Fact]
    [Trait("Rule", "ADM-07")]
    public async Task ADM07_change_takes_effect_at_once()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();
        var (botId, _) = await app.AddBotAsync();
        var (user, _) = await app.LoginAsync();

        var entry = await (await admin.SetAsync("Relay:MaxTextLength", "10")).JsonAsync();
        var response = await user.PostAsJsonAsync($"/api/bots/{botId}/messages", new { text = "12345678901" }, AdminApp.Cancel);

        Assert.Equal("10", entry.GetProperty("value").GetString());
        Assert.True(entry.GetProperty("changed").GetBoolean());
        Assert.Equal("4096", entry.GetProperty("start").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("text_too_long", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "ADM-07")]
    public async Task ADM07_retention_change_reaches_the_sweep()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();
        await PostgresServer.ExecuteAsync(app.ConnectionString, $"""
            INSERT INTO admin.access_log (actor, actor_id, address, at)
            VALUES ('Bot', 9, '203.0.113.7', '{app.Time.GetUtcNow().AddDays(-190):O}')
            """);

        (await admin.SetAsync("Admin:AccessLogRetentionDays", "200")).EnsureSuccessStatusCode();
        await app.Services.GetRequiredService<AccessLogService>().SweepOnceAsync(AdminApp.Cancel);

        Assert.Contains("Bot 9 203.0.113.7", await app.AccessLogAsync());
    }

    [Fact]
    [Trait("Rule", "ADM-07")]
    public async Task ADM07_value_survives_a_restart()
    {
        var database = await postgres.NewDatabaseAsync();
        var settings = new Dictionary<string, string> { ["Identity:AdminPhones"] = AdminApp.AdminPhone };
        await using (var first = new DalanApp(database, settings))
        {
            await first.ReadyAsync();
            var (admin, _) = await first.AdminAsync();
            (await admin.SetAsync("Relay:MaxTextLength", "10")).EnsureSuccessStatusCode();
        }

        await using var second = new DalanApp(database, settings);
        await second.ReadyAsync();
        var options = second.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Dalan.Relay.RelayOptions>>();

        Assert.Equal(10, options.CurrentValue.MaxTextLength);
    }

    [Theory]
    [Trait("Rule", "ADM-07")]
    [InlineData("Relay:MaxTextLength", "0")]
    [InlineData("Relay:MaxTextLength", "abc")]
    [InlineData("Relay:MaxTextLength", "-5")]
    [InlineData("Relay:LatestCommandWins", "yes")]
    public async Task ADM07_bad_value_is_refused(string key, string value)
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();
        var before = (await admin.SettingAsync(key)).GetProperty("value").GetString();

        var response = await admin.SetAsync(key, value);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_value", await response.ErrorCodeAsync());
        Assert.Equal(before, (await admin.SettingAsync(key)).GetProperty("value").GetString());
    }

    [Fact]
    [Trait("Rule", "ADM-07")]
    public async Task ADM07_text_longer_than_the_limit_is_refused()
    {
        await using var app = await AdminApp.StartAsync(postgres, ("Admin:MaxSettingLength", "5"));
        var (admin, _) = await app.AdminAsync();

        var response = await admin.SetAsync("Notification:Push:AllowedHosts", "abcdef");

        Assert.Equal("bad_value", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "ADM-07")]
    public async Task ADM07_switch_is_stored_in_lower_case()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();

        var entry = await (await admin.SetAsync("Relay:LatestCommandWins", "False")).JsonAsync();

        Assert.Equal("false", entry.GetProperty("value").GetString());
    }

    [Theory]
    [Trait("Rule", "ADM-07")]
    [InlineData("Identity:SigningKey")]
    [InlineData("Dalan:TrustedProxies")]
    [InlineData("Identity:AdminPhones")]
    [InlineData("Nothing:Here")]
    public async Task ADM07_key_that_is_not_listed_gets_404(string key)
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();

        var response = await admin.SetAsync(key, "1");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("setting_not_found", await response.ErrorCodeAsync());
        Assert.Equal(0, await PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT count(*) FROM admin.settings"));
    }

    [Fact]
    [Trait("Rule", "ADM-08")]
    public async Task ADM08_reset_brings_the_start_value_back()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();
        var (botId, _) = await app.AddBotAsync();
        var (user, _) = await app.LoginAsync();
        (await admin.SetAsync("Relay:MaxTextLength", "10")).EnsureSuccessStatusCode();

        var entry = await (await admin.DeleteAsync("/api/admin/settings/Relay:MaxTextLength", AdminApp.Cancel)).JsonAsync();
        var response = await user.PostAsJsonAsync($"/api/bots/{botId}/messages", new { text = "12345678901" }, AdminApp.Cancel);

        Assert.Equal("4096", entry.GetProperty("value").GetString());
        Assert.False(entry.GetProperty("changed").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Rule", "ADM-08")]
    public async Task ADM08_reset_of_unknown_key_gets_404()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();

        var response = await admin.DeleteAsync("/api/admin/settings/Identity:SigningKey", AdminApp.Cancel);

        Assert.Equal("setting_not_found", await response.ErrorCodeAsync());
    }
}
