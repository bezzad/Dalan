using System.Net;
using System.Net.Http.Json;
using Dalan.Testing;

namespace Dalan.Admin.Tests;

public class ShowcaseReviewTests(PostgresServer postgres)
{
    private static CancellationToken Cancel => AdminApp.Cancel;

    /// <summary>The admin's browser and a bot of the admin with a pending entry.</summary>
    private static async Task<(HttpClient Admin, long BotId)> PendingAsync(DalanApp app)
    {
        var (admin, adminId) = await app.AdminAsync();
        var (botId, _) = await app.AddBotAsync("game_bot", adminId);
        (await admin.PutAsJsonAsync($"/api/builder/bots/{botId}/showcase", new { description = "A game", category = "games" }, Cancel))
            .EnsureSuccessStatusCode();
        return (admin, botId);
    }

    private static Task<HttpResponseMessage> ReviewAsync(HttpClient admin, long botId, bool approve) =>
        admin.PostAsJsonAsync($"/api/admin/showcase/{botId}", new { approve }, Cancel);

    private static async Task<long[]> PublicIdsAsync(DalanApp app) =>
        [.. (await app.CreateClient().JsonAsync("/api/showcase")).Rows("bots").Select(b => b.GetProperty("id").GetInt64())];

    private static async Task<string?[]> AuditActionsAsync(HttpClient admin) =>
        [.. (await admin.JsonAsync("/api/admin/audit")).Rows().Select(r => r.GetProperty("action").GetString())];

    [Fact]
    [Trait("Rule", "ADM-13")]
    public async Task ADM13_pending_entries_are_listed_for_the_admin()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, botId) = await PendingAsync(app);

        var entry = Assert.Single((await admin.JsonAsync("/api/admin/showcase")).Rows("entries"));

        Assert.Equal(botId, entry.GetProperty("bot_id").GetInt64());
        Assert.Equal("game_bot", entry.GetProperty("username").GetString());
        Assert.Equal("A game", entry.GetProperty("description").GetString());
        Assert.Equal("games", entry.GetProperty("category").GetString());
        Assert.False(entry.GetProperty("icon").GetBoolean());
        Assert.False(entry.GetProperty("suspended").GetBoolean());
    }

    [Fact]
    [Trait("Rule", "ADM-13")]
    public async Task ADM13_approve_lists_the_bot()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, botId) = await PendingAsync(app);
        Assert.Empty(await PublicIdsAsync(app));

        Assert.Equal(HttpStatusCode.NoContent, (await ReviewAsync(admin, botId, approve: true)).StatusCode);

        Assert.Equal(new[] { botId }, await PublicIdsAsync(app));
        Assert.Contains("showcase.approve", await AuditActionsAsync(admin));
        Assert.Empty((await admin.JsonAsync("/api/admin/showcase")).Rows("entries"));
    }

    [Fact]
    [Trait("Rule", "ADM-13")]
    public async Task ADM13_refuse_keeps_the_bot_out()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, botId) = await PendingAsync(app);

        Assert.Equal(HttpStatusCode.NoContent, (await ReviewAsync(admin, botId, approve: false)).StatusCode);

        Assert.Empty(await PublicIdsAsync(app));
        Assert.Empty((await admin.JsonAsync("/api/admin/showcase")).Rows("entries"));
        Assert.Contains("showcase.refuse", await AuditActionsAsync(admin));
        var own = (await admin.JsonAsync("/api/builder/bots")).Rows("bots").Single();
        Assert.Equal("refused", own.GetProperty("showcase").GetProperty("state").GetString());
    }

    [Fact]
    [Trait("Rule", "ADM-13")]
    public async Task ADM13_bot_with_no_entry_gets_404()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, adminId) = await app.AdminAsync();
        var (botId, _) = await app.AddBotAsync("game_bot", adminId);

        var response = await ReviewAsync(admin, botId, approve: true);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("showcase_not_found", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "ADM-13")]
    public async Task ADM13_not_an_admin_gets_403()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (browser, _) = await app.LoginAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await browser.GetAsync("/api/admin/showcase", Cancel)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "ADM-14")]
    public async Task ADM14_visitor_reads_the_numbers_of_the_public_pages()
    {
        await using var app = await AdminApp.StartAsync(postgres);

        var numbers = await app.CreateClient().JsonAsync("/api/public/settings");

        Assert.Equal(200, numbers.GetProperty("history_messages").GetInt32());
        Assert.Equal(30, numbers.GetProperty("history_days").GetInt32());
        Assert.Equal(180, numbers.GetProperty("access_log_days").GetInt32());
        Assert.Equal(30, numbers.GetProperty("file_days").GetInt32());
        Assert.Equal(60, numbers.GetProperty("update_expiry_seconds").GetInt32());
    }

    [Fact]
    [Trait("Rule", "CLI-72")]
    public async Task CLI72_visitor_reads_the_running_version_from_the_public_settings()
    {
        await using var app = await AdminApp.StartAsync(postgres, ("Dalan:Version", "v1.1.3"));

        var numbers = await app.CreateClient().JsonAsync("/api/public/settings");

        Assert.Equal("v1.1.3", numbers.GetProperty("version").GetString());
    }

    [Fact]
    [Trait("Rule", "ADM-14")]
    public async Task ADM14_a_panel_change_shows_at_once()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();

        (await admin.SetAsync("Relay:HistoryDays", "60")).EnsureSuccessStatusCode();

        Assert.Equal(60, (await app.CreateClient().JsonAsync("/api/public/settings")).GetProperty("history_days").GetInt32());
    }

    [Fact]
    [Trait("Rule", "ADM-15")]
    public async Task ADM15_visitor_reads_llms_txt()
    {
        await using var app = await AdminApp.StartAsync(postgres);

        var response = await app.CreateClient().GetAsync("/llms.txt", TestContext.Current.CancellationToken);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var numbers = await app.CreateClient().JsonAsync("/api/public/settings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        Assert.StartsWith("# Dalan", text, StringComparison.Ordinal);
        Assert.Contains("https://api.dalanet.ir/bot<token>/<method>", text, StringComparison.Ordinal);
        Assert.Contains("https://dalanet.ir/api/mcp", text, StringComparison.Ordinal);
        Assert.Contains("https://dalanet.ir/dalan.mjs", text, StringComparison.Ordinal);
        Assert.Contains("access key", text, StringComparison.Ordinal);
        Assert.DoesNotContain("{site}", text, StringComparison.Ordinal);
        Assert.Equal("https://dalanet.ir", numbers.GetProperty("site_url").GetString());
        Assert.Equal("https://api.dalanet.ir", numbers.GetProperty("bot_api_url").GetString());
    }

    [Fact]
    [Trait("Rule", "CLI-30")]
    public async Task CLI30_llms_txt_says_the_access_key_is_made_in_the_account()
    {
        await using var app = await AdminApp.StartAsync(postgres);

        var text = await app.CreateClient().GetStringAsync("/llms.txt", TestContext.Current.CancellationToken);

        Assert.Contains("opens \"Account\", and makes an access key", text, StringComparison.Ordinal);
        Assert.DoesNotContain("opens \"My bots\", and makes an access key", text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "CLI-41")]
    public async Task CLI41_llms_txt_never_names_iran()
    {
        await using var app = await AdminApp.StartAsync(postgres);

        var text = await app.CreateClient().GetStringAsync("/llms.txt", TestContext.Current.CancellationToken);

        Assert.DoesNotContain("iran", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Rule", "ADM-15")]
    public async Task ADM15_a_panel_change_of_an_address_shows_at_once()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();

        (await admin.SetAsync("Admin:BotApiUrl", "https://bots.example.ir")).EnsureSuccessStatusCode();

        var text = await app.CreateClient().GetStringAsync("/llms.txt", TestContext.Current.CancellationToken);
        Assert.Contains("https://bots.example.ir/bot<token>/<method>", text, StringComparison.Ordinal);
        Assert.DoesNotContain("api.dalanet.ir", text, StringComparison.Ordinal);
        Assert.Equal("https://bots.example.ir",
            (await app.CreateClient().JsonAsync("/api/public/settings")).GetProperty("bot_api_url").GetString());
    }
}
