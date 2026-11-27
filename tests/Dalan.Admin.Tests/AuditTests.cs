using System.Net.Http.Json;
using Dalan.Testing;

namespace Dalan.Admin.Tests;

public class AuditTests(PostgresServer postgres)
{
    [Fact]
    [Trait("Rule", "ADM-09")]
    public async Task ADM09_setting_change_is_recorded_with_old_and_new_value()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, adminId) = await app.AdminAsync();

        (await admin.SetAsync("Relay:MaxTextLength", "10")).EnsureSuccessStatusCode();
        var row = (await admin.JsonAsync("/api/admin/audit")).Rows()[0];

        Assert.Equal(adminId, row.GetProperty("user_id").GetInt64());
        Assert.Equal("setting.set", row.GetProperty("action").GetString());
        Assert.Equal("Relay:MaxTextLength", row.GetProperty("target").GetString());
        Assert.Equal("4096 -> 10", row.GetProperty("detail").GetString());
        Assert.Equal(app.Time.GetUtcNow(), row.GetProperty("at").GetDateTimeOffset());
    }

    [Fact]
    [Trait("Rule", "ADM-09")]
    public async Task ADM09_every_admin_action_is_recorded()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();
        var (_, userId) = await app.LoginAsync();
        var (botId, _) = await app.AddBotAsync();

        (await admin.DeleteAsync("/api/admin/settings/Relay:MaxTextLength", AdminApp.Cancel)).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/admin/users/{userId}/builder", new { approve = true }, AdminApp.Cancel)).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/admin/users/{userId}/builder", new { approve = false }, AdminApp.Cancel)).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/admin/users/{userId}/suspend", new { suspended = true }, AdminApp.Cancel)).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/admin/users/{userId}/suspend", new { suspended = false }, AdminApp.Cancel)).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/admin/bots/{botId}/suspend", new { suspended = true }, AdminApp.Cancel)).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/admin/bots/{botId}/suspend", new { suspended = false }, AdminApp.Cancel)).EnsureSuccessStatusCode();

        // The admin of a test turned the second factor on first (SEC-15); that row is not an admin action.
        var actions = (await admin.JsonAsync("/api/admin/audit")).Rows().Select(r => r.GetProperty("action").GetString())
            .Where(action => action != "totp.on").Reverse();
        Assert.Equal(
            ["setting.reset", "builder.approve", "builder.refuse", "user.suspend", "user.unsuspend", "bot.suspend", "bot.unsuspend"],
            actions);
    }

    [Fact]
    [Trait("Rule", "ADM-09")]
    public async Task ADM09_pages_newest_first()
    {
        await using var app = await AdminApp.StartAsync(postgres, ("Admin:PageSize", "2"));
        var (admin, _) = await app.AdminAsync();
        foreach (var value in new[] { "10", "11", "12" })
            (await admin.SetAsync("Relay:MaxTextLength", value)).EnsureSuccessStatusCode();

        var first = (await admin.JsonAsync("/api/admin/audit")).Rows();
        var second = (await admin.JsonAsync($"/api/admin/audit?before={first[^1].GetProperty("id").GetInt64()}")).Rows();

        Assert.Equal(["11 -> 12", "10 -> 11"], first.Select(r => r.GetProperty("detail").GetString()));
        // The row after it is the admin turning the second factor on (SEC-15).
        Assert.Equal(["4096 -> 10", null], second.Select(r => r.GetProperty("detail").GetString()));
    }
}
