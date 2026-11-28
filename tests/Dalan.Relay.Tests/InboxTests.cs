using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Notification.Contracts;
using Dalan.Notification.Inbox;
using Dalan.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Relay.Tests;

/// <summary>NTF-12 and NTF-13: the inbox under the bell, its push, and the SMS when no push reaches.</summary>
public class InboxTests(PostgresServer postgres)
{
    private const string SiteUrl = "https://dalan.test";
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private async Task<DalanApp> StartAsync(params (string Key, string Value)[] settings)
    {
        var all = new Dictionary<string, string> { ["Admin:SiteUrl"] = SiteUrl };
        foreach (var (key, value) in settings) all[key] = value;
        var app = new DalanApp(await postgres.NewDatabaseAsync(), all);
        await app.ReadyAsync();
        return app;
    }

    private async Task<(DalanApp App, PushTests.FakePushService Service)> StartWithPushAsync(params (string Key, string Value)[] settings)
    {
        var service = new PushTests.FakePushService();
        var app = PushTests.App(await postgres.NewDatabaseAsync(), service,
            PushTests.PushSettings([("Admin:SiteUrl", SiteUrl), .. settings]));
        await app.ReadyAsync();
        return (app, service);
    }

    private static IInbox Inbox(DalanApp app) => app.Services.GetRequiredService<IInbox>();

    private static Task<JsonElement> ListAsync(HttpClient browser, string query = "") =>
        browser.GetFromJsonAsync<JsonElement>("/api/inbox" + query, Cancel);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++) await Task.Delay(50, Cancel);
        Assert.True(condition());
    }

    // ---------- NTF-12 ----------

    [Fact]
    [Trait("Rule", "NTF-12")]
    public async Task NTF12_a_notice_is_listed_then_marked_read()
    {
        await using var app = await StartAsync();
        var (browser, sara) = await app.LoginAsync("Sara");
        DalanApp.From(browser, "198.51.100.7:40000");

        await Inbox(app).AddAsync(sara, "سایت جدید shop.dalans.ir", "Ali (7)", "/dashboard/admin/users/7", Cancel);

        var list = await ListAsync(browser);
        Assert.Equal(1, list.GetProperty("unread").GetInt32());
        var notice = Assert.Single(list.GetProperty("notices").EnumerateArray());
        Assert.Equal("سایت جدید shop.dalans.ir", notice.GetProperty("title").GetString());
        Assert.Equal("Ali (7)", notice.GetProperty("text").GetString());
        Assert.Equal("/dashboard/admin/users/7", notice.GetProperty("link").GetString());
        Assert.False(notice.GetProperty("read").GetBoolean());
        Assert.Equal(app.Time.GetUtcNow(), notice.GetProperty("at").GetDateTimeOffset());

        var read = await browser.PostAsJsonAsync("/api/inbox/read", new { up_to = notice.GetProperty("id").GetInt64() }, Cancel);

        Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);
        var after = await ListAsync(browser);
        Assert.Equal(0, after.GetProperty("unread").GetInt32());
        Assert.True(after.GetProperty("notices")[0].GetProperty("read").GetBoolean());
        Assert.Contains($"User {sara} 198.51.100.7 40000", await app.AccessLogAsync());
    }

    [Fact]
    [Trait("Rule", "NTF-12")]
    public async Task NTF12_only_ones_own()
    {
        await using var app = await StartAsync();
        var (sara, saraId) = await app.LoginAsync("Sara");
        var (ali, _) = await app.LoginAsync("Ali");
        await Inbox(app).AddAsync(saraId, "For Sara", "", null, Cancel);
        var saraNotice = (await ListAsync(sara)).GetProperty("notices")[0].GetProperty("id").GetInt64();

        var list = await ListAsync(ali);
        // Ali's "read" never touches Sara's notice.
        (await ali.PostAsJsonAsync("/api/inbox/read", new { up_to = saraNotice }, Cancel)).EnsureSuccessStatusCode();

        Assert.Empty(list.GetProperty("notices").EnumerateArray());
        Assert.Equal(0, list.GetProperty("unread").GetInt32());
        Assert.Equal(1, (await ListAsync(sara)).GetProperty("unread").GetInt32());
    }

    [Fact]
    [Trait("Rule", "NTF-12")]
    public async Task NTF12_pages_newest_first()
    {
        await using var app = await StartAsync(("Notification:InboxPageSize", "2"));
        var (browser, userId) = await app.LoginAsync();
        foreach (var title in new[] { "one", "two", "three" }) await Inbox(app).AddAsync(userId, title, "", null, Cancel);

        var first = await ListAsync(browser);
        var titles = first.GetProperty("notices").EnumerateArray().Select(n => n.GetProperty("title").GetString()).ToArray();
        var last = first.GetProperty("notices")[1].GetProperty("id").GetInt64();
        var second = await ListAsync(browser, $"?before={last}");

        Assert.Equal(["three", "two"], titles);
        Assert.Equal(3, first.GetProperty("unread").GetInt32());
        Assert.Equal("one", Assert.Single(second.GetProperty("notices").EnumerateArray()).GetProperty("title").GetString());
    }

    [Fact]
    [Trait("Rule", "NTF-12")]
    public async Task NTF12_needs_a_session()
    {
        await using var app = await StartAsync();

        var list = await app.Browser().GetAsync("/api/inbox", Cancel);
        var read = await app.Browser().PostAsJsonAsync("/api/inbox/read", new { up_to = 1 }, Cancel);

        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
    }

    [Fact]
    [Trait("Rule", "NTF-12")]
    public async Task NTF12_old_notices_are_removed()
    {
        await using var app = await StartAsync(("Notification:InboxDays", "90"));
        var userId = await app.AddUserAsync();
        await Inbox(app).AddAsync(userId, "old", "", null, Cancel);
        app.Time.Advance(TimeSpan.FromDays(89));
        await Inbox(app).AddAsync(userId, "new", "", null, Cancel);
        app.Time.Advance(TimeSpan.FromDays(2));

        await app.Services.GetRequiredService<InboxSweep>().SweepOnceAsync(Cancel);

        // The session has ended in 91 days, so the database is read directly.
        Assert.Equal(["new"], await PostgresServer.ScalarAsync<string[]>(app.ConnectionString,
            $"SELECT array_agg(title) FROM notification.inbox WHERE user_id = {userId}"));
    }

    // ---------- NTF-13 ----------

    [Fact]
    [Trait("Rule", "NTF-13")]
    public async Task NTF13_push_works_and_no_sms_is_sent()
    {
        var (app, service) = await StartWithPushAsync();
        await using var _ = app;
        var (browser, userId) = await app.LoginAsync();
        using var device = new PushTests.Device { Endpoint = "https://push.test/a" };
        (await browser.PostAsJsonAsync("/api/push/subscribe", device.Json, Cancel)).EnsureSuccessStatusCode();

        await Inbox(app).AddAsync(userId, "New site shop.dalans.ir", "Ali (7)", "/dashboard/admin/users/7", Cancel);

        var sent = await service.NextAsync();
        var payload = JsonElement.Parse(device.Decrypt(sent.Body));
        Assert.Equal("New site shop.dalans.ir", payload.GetProperty("title").GetString());
        Assert.Equal("/dashboard/admin/users/7", payload.GetProperty("url").GetString());
        await Task.Delay(300, Cancel);
        Assert.Empty(app.Sms.Notices);
    }

    [Theory]
    [Trait("Rule", "NTF-13")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NTF13_push_cannot_reach_so_one_short_sms_goes_and_is_recorded(bool hangs)
    {
        var (app, service) = await StartWithPushAsync(("Notification:Push:TimeoutSeconds", "1"), ("Notification:InboxSmsLength", "40"));
        await using var _ = app;
        service.Status = _ => HttpStatusCode.InternalServerError;
        service.Hangs = hangs;
        var (browser, userId) = await app.LoginAsync("Sara", "09121230001");
        using var device = new PushTests.Device { Endpoint = "https://push.test/a" };
        (await browser.PostAsJsonAsync("/api/push/subscribe", device.Json, Cancel)).EnsureSuccessStatusCode();

        await Inbox(app).AddAsync(userId, "A rather long title for a short SMS", "text", "/x/1", Cancel);

        await WaitUntilAsync(() => app.Sms.Notices.Count == 1);
        var (phone, text) = app.Sms.Notices[0];
        Assert.Equal("+989121230001", phone);
        Assert.Equal(40, text.Length);
        Assert.EndsWith(" " + SiteUrl + "/x/1", text, StringComparison.Ordinal);
        Assert.StartsWith("A rather", text, StringComparison.Ordinal);
        Assert.Contains("Phone +989121230001 127.0.0.1", await app.AccessLogAsync());
        await Task.Delay(300, Cancel);
        Assert.Single(app.Sms.Notices);
    }

    [Fact]
    [Trait("Rule", "NTF-13")]
    public async Task NTF13_no_push_device_means_an_sms()
    {
        await using var app = await StartAsync();
        var (_, userId) = await app.LoginAsync("Sara", "09121230002");

        await Inbox(app).AddAsync(userId, "Hello", "text", null, Cancel);

        await WaitUntilAsync(() => app.Sms.Notices.Count == 1);
        Assert.Equal(("+989121230002", "Hello"), app.Sms.Notices[0]);
    }

    [Fact]
    [Trait("Rule", "NTF-13")]
    public async Task NTF13_daily_limit()
    {
        await using var app = await StartAsync(("Notification:InboxSmsPerDay", "2"));
        var (browser, userId) = await app.LoginAsync();
        for (var i = 0; i < 2; i++)
        {
            await Inbox(app).AddAsync(userId, $"n{i}", "", null, Cancel);
            await WaitUntilAsync(() => app.Sms.Notices.Count == i + 1);
        }

        await Inbox(app).AddAsync(userId, "over", "", null, Cancel);
        await WaitUntilAsync(() => app.Logs.Lines.Any(line => line.Text.Contains("daily limit", StringComparison.Ordinal)));

        Assert.Equal(2, app.Sms.Notices.Count);
        Assert.Equal(3, (await ListAsync(browser)).GetProperty("notices").GetArrayLength());

        // A day later there is room again.
        app.Time.Advance(TimeSpan.FromHours(24) + TimeSpan.FromMinutes(1));
        await Inbox(app).AddAsync(userId, "next day", "", null, Cancel);
        await WaitUntilAsync(() => app.Sms.Notices.Count == 3);
    }

    [Fact]
    [Trait("Rule", "NTF-13")]
    public async Task NTF13_a_failed_sms_does_not_count_against_the_day()
    {
        await using var app = await StartAsync(("Notification:InboxSmsPerDay", "1"));
        var (_, userId) = await app.LoginAsync();
        app.Sms.Fails = true;
        await Inbox(app).AddAsync(userId, "lost", "", null, Cancel);
        await WaitUntilAsync(() => app.Sms.NoticeTries == 1);
        // The failed send gives its place of the day back right after it.
        await Task.Delay(300, Cancel);
        app.Sms.Fails = false;

        await Inbox(app).AddAsync(userId, "kept", "", null, Cancel);

        await WaitUntilAsync(() => app.Sms.Notices.Count == 1);
        Assert.Equal("kept", app.Sms.Notices[0].Text);
    }

    [Fact]
    [Trait("Rule", "NTF-13")]
    public async Task NTF13_a_user_with_no_phone_gets_no_sms()
    {
        await using var app = await StartAsync();
        var (browser, userId) = await app.LoginAsync();
        await PostgresServer.ExecuteAsync(app.ConnectionString, $"UPDATE identity.users SET phone = NULL, email = 'x@example.com' WHERE id = {userId}");

        await Inbox(app).AddAsync(userId, "Hello", "", null, Cancel);

        await Task.Delay(500, Cancel);
        Assert.Empty(app.Sms.Notices);
        Assert.Equal(1, (await ListAsync(browser)).GetProperty("unread").GetInt32());
    }

    [Theory]
    [Trait("Rule", "NTF-13")]
    [InlineData("Hi", null, 120, "Hi")]
    [InlineData("Hi", "https://d.ir/a", 120, "Hi https://d.ir/a")]
    [InlineData("A long title", "https://d.ir/a", 20, "A lo… https://d.ir/a")]
    [InlineData("Title", "https://d.ir/a-very-long-link", 10, "https://d…")]
    public void NTF13_sms_text_keeps_the_link_whole(string title, string? link, int max, string expected)
    {
        var text = InboxService.SmsText(title, link, max);

        Assert.Equal(expected, text);
        Assert.True(text.Length <= max);
    }
}
