using System.Net;
using System.Net.Http.Json;
using Dalan.Relay.Bots;
using Dalan.Sites.Tunnel;
using Dalan.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Admin.Tests;

public class OverviewTests(PostgresServer postgres)
{
    private static CancellationToken Cancel => AdminApp.Cancel;

    [Fact]
    [Trait("Rule", "ADM-19")]
    public async Task ADM19_overview_counts()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, adminId) = await app.AdminAsync();

        // Users: the admin, two who ask to be builders, and one from ten days ago.
        foreach (var name in new[] { "Sara", "Reza" })
        {
            var (user, _) = await app.LoginAsync(name);
            (await user.PostAsync("/api/account/builder", null, Cancel)).EnsureSuccessStatusCode();
        }
        var old = await app.AddUserAsync("Old");
        await PostgresServer.ExecuteAsync(app.ConnectionString,
            $"UPDATE identity.users SET created_at = '{app.Time.GetUtcNow().AddDays(-10):O}' WHERE id = {old}");

        // Bots: three, one polling now, one waiting for the showcase review.
        var (pollingBot, _) = await app.AddBotAsync("poll_bot", adminId);
        var (showcaseBot, _) = await app.AddBotAsync("game_bot", adminId);
        await app.AddBotAsync("quiet_bot", adminId);
        await app.Services.GetRequiredService<BotPresence>().PollStartsAsync(pollingBot, Cancel);
        (await admin.PutAsJsonAsync($"/api/builder/bots/{showcaseBot}/showcase", new { description = "A game", category = "games" }, Cancel))
            .EnsureSuccessStatusCode();

        // Support: two messages, one read.
        foreach (var text in new[] { "First", "Second" })
            (await DalanApp.From(app.Browser(), "203.0.113.9:40000").PostAsJsonAsync("/api/contact", new { email = "ali@example.com", text }, Cancel)).EnsureSuccessStatusCode();
        var first = (await admin.JsonAsync("/api/admin/contact")).Rows()[^1].GetProperty("id").GetInt64();
        (await admin.PostAsJsonAsync($"/api/admin/contact/{first}/read", new { read = true }, Cancel)).EnsureSuccessStatusCode();

        // Sites: one with a live agent session.
        app.Services.GetRequiredService<TunnelHub>().Open(adminId, ["mysite"]);

        var overview = await admin.JsonAsync("/api/admin/overview");

        Assert.Equal(4, overview.GetProperty("users").GetInt32());
        Assert.Equal(3, overview.GetProperty("users_new_7d").GetInt32());
        Assert.Equal(3, overview.GetProperty("bots").GetInt32());
        Assert.Equal(1, overview.GetProperty("bots_online").GetInt32());
        Assert.Equal(2, overview.GetProperty("builder_requests").GetInt32());
        Assert.Equal(1, overview.GetProperty("showcase_requests").GetInt32());
        Assert.Equal(1, overview.GetProperty("unread_messages").GetInt32());
        Assert.Equal(1, overview.GetProperty("sites_online").GetInt32());
    }

    [Fact]
    [Trait("Rule", "ADM-19")]
    public async Task ADM19_overview_needs_an_admin()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (user, _) = await app.LoginAsync("Sara");

        var response = await user.GetAsync("/api/admin/overview", Cancel);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("not_admin", await response.ErrorCodeAsync());
    }
}
