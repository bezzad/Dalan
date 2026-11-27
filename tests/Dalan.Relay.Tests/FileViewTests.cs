using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Testing;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Dalan.Relay.Tests;

/// <summary>An HTML file shown in the app from the view host (REL-57, SEC-16).</summary>
public class FileViewTests(PostgresServer postgres)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private const string Report = "<html><body><script>document.title = 'page 1'</script>report</body></html>";

    private const string Csp =
        "sandbox allow-scripts allow-popups; default-src 'none'; script-src 'unsafe-inline' data: blob:; " +
        "style-src 'unsafe-inline' data: blob:; img-src data: blob:; font-src data:; media-src data: blob:; " +
        "connect-src 'none'; form-action 'none'; base-uri 'none'; frame-ancestors https://dalanet.ir";

    private static Task<DalanApp> StartAsync(PostgresServer postgres, params (string Key, string Value)[] settings)
    {
        var all = new Dictionary<string, string>
        {
            ["Sites:Domain"] = "dalans.ir",
            ["Sites:Scheme"] = "https",
            ["Admin:SiteUrl"] = "https://dalanet.ir/",
            ["Relay:FileViewHost"] = "",
        };
        foreach (var (key, value) in settings) all[key] = value;
        return RelayApp.StartAsync(postgres, [.. all.Select(s => (s.Key, s.Value))]);
    }

    /// <summary>The bot sends a file; gives the message id and the file id.</summary>
    private static async Task<(long MessageId, string FileId)> BotSendsAsync(Chat chat, string name, string? type, string body = Report)
    {
        var response = await chat.BotUploadAsync(System.Text.Encoding.UTF8.GetBytes(body), name, "document", type: type);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(Cancel);
        return (json.GetProperty("id").GetInt64(), json.GetProperty("file").GetProperty("id").GetString()!);
    }

    private static Task<HttpResponseMessage> AskLinkAsync(HttpClient browser, long botId, string fileId) =>
        browser.PostAsync($"/api/bots/{botId}/files/{fileId}/view", null, Cancel);

    private static async Task<(string Url, DateTimeOffset ExpiresAt)> LinkAsync(Chat chat, string fileId)
    {
        var response = await AskLinkAsync(chat.Browser, chat.BotId, fileId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(Cancel);
        return (json.GetProperty("url").GetString()!, json.GetProperty("expires_at").GetDateTimeOffset());
    }

    /// <summary>A browser tab on the view host, from its own address, with no cookie of the app.</summary>
    private static HttpClient ViewTab(DalanApp app, string endPoint = "203.0.113.20:6000") =>
        DalanApp.From(app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://view.dalans.ir") }), endPoint);

    [Fact]
    [Trait("Rule", "REL-57")]
    public async Task REL57_link_for_a_report_on_the_view_host_and_in_the_access_log()
    {
        await using var app = await StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var (_, fileId) = await BotSendsAsync(chat, "report.html", "text/html");
        DalanApp.From(chat.Browser, "198.51.100.30:7000");

        var (url, expiresAt) = await LinkAsync(chat, fileId);

        Assert.StartsWith("https://view.dalans.ir/v/", url, StringComparison.Ordinal);
        Assert.Equal(app.Time.GetUtcNow().AddMinutes(10), expiresAt);
        Assert.Contains($"User {chat.UserId} 198.51.100.30 7000", await app.AccessLogAsync());
    }

    [Theory]
    [Trait("Rule", "REL-57")]
    [InlineData("report.htm", "application/octet-stream")]
    [InlineData("REPORT.HTML", null)]
    [InlineData("page", "text/html")]
    public async Task REL57_html_by_type_or_by_name_when_the_type_is_not_clear(string name, string? type)
    {
        await using var app = await StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var (_, fileId) = await BotSendsAsync(chat, name, type);

        Assert.Equal(HttpStatusCode.OK, (await AskLinkAsync(chat.Browser, chat.BotId, fileId)).StatusCode);
    }

    [Theory]
    [Trait("Rule", "REL-57")]
    [InlineData("a.pdf", "application/pdf")]
    [InlineData("a.html", "text/plain")]
    [InlineData("a.svg", "image/svg+xml")]
    public async Task REL57_not_html_is_400(string name, string type)
    {
        await using var app = await StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var (_, fileId) = await BotSendsAsync(chat, name, type);

        var response = await AskLinkAsync(chat.Browser, chat.BotId, fileId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("not_html", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "REL-57")]
    public async Task REL57_another_users_file_is_404()
    {
        await using var app = await StartAsync(postgres);
        var sara = await Chat.StartAsync(app);
        var (ali, _) = await app.LoginAsync("Ali");
        await ali.SayAsync(sara.BotId, "/start");
        var (_, fileId) = await BotSendsAsync(sara, "report.html", "text/html");

        var response = await AskLinkAsync(ali, sara.BotId, fileId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("file_not_found", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "REL-57")]
    public async Task REL57_set_view_host_wins_over_the_sites_domain()
    {
        await using var app = await StartAsync(postgres, ("Relay:FileViewHost", "files.example.org"), ("Relay:FileViewMinutes", "3"));
        var chat = await Chat.StartAsync(app);
        var (_, fileId) = await BotSendsAsync(chat, "report.html", "text/html");

        var (url, expiresAt) = await LinkAsync(chat, fileId);

        Assert.StartsWith("https://files.example.org/v/", url, StringComparison.Ordinal);
        Assert.Equal(app.Time.GetUtcNow().AddMinutes(3), expiresAt);
    }

    [Fact]
    [Trait("Rule", "REL-57")]
    public async Task REL57_viewer_off_is_503()
    {
        await using var app = await StartAsync(postgres, ("Sites:Domain", ""));
        var chat = await Chat.StartAsync(app);
        var (_, fileId) = await BotSendsAsync(chat, "report.html", "text/html");

        var response = await AskLinkAsync(chat.Browser, chat.BotId, fileId);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("viewer_off", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "REL-57")]
    [Trait("Rule", "REL-18")]
    public async Task REL57_settings_say_whether_the_viewer_is_on()
    {
        await using var on = await StartAsync(postgres);
        await using var off = await StartAsync(postgres, ("Sites:Domain", ""));

        var onSettings = await (await on.LoginAsync()).Browser.JsonAsync("/api/settings");
        var offSettings = await (await off.LoginAsync()).Browser.JsonAsync("/api/settings");

        Assert.True(onSettings.GetProperty("file_view").GetBoolean());
        Assert.False(offSettings.GetProperty("file_view").GetBoolean());
    }

    [Fact]
    [Trait("Rule", "SEC-16")]
    public async Task SEC16_link_gives_the_file_with_the_walls_and_no_cookie()
    {
        await using var app = await StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var (_, fileId) = await BotSendsAsync(chat, "report.html", "text/html");
        var (url, _) = await LinkAsync(chat, fileId);
        var tab = ViewTab(app);
        tab.DefaultRequestHeaders.Add("Cookie", "__Host-dalan_session=stolen");

        var response = await tab.GetAsync(new Uri(url).PathAndQuery, Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Report, await response.Content.ReadAsStringAsync(Cancel));
        Assert.Equal("text/html; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        Assert.Equal(Csp, Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
        Assert.DoesNotContain("allow-same-origin", Csp, StringComparison.Ordinal);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.Equal("private, no-store", response.Headers.NonValidated["Cache-Control"].ToString());
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.Contains($"User {chat.UserId} 203.0.113.20 6000", await app.AccessLogAsync());
    }

    [Fact]
    [Trait("Rule", "SEC-16")]
    public async Task SEC16_expired_link_is_404()
    {
        await using var app = await StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var (_, fileId) = await BotSendsAsync(chat, "report.html", "text/html");
        var (url, _) = await LinkAsync(chat, fileId);

        app.Time.Advance(TimeSpan.FromMinutes(11));
        var response = await ViewTab(app).GetAsync(new Uri(url).PathAndQuery, Cancel);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        // The token named its user, so even this answer is in the access log.
        Assert.Contains($"User {chat.UserId} 203.0.113.20 6000", await app.AccessLogAsync());
    }

    [Fact]
    [Trait("Rule", "SEC-16")]
    public async Task SEC16_deleted_message_closes_the_link()
    {
        await using var app = await StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var (messageId, fileId) = await BotSendsAsync(chat, "report.html", "text/html");
        var (url, _) = await LinkAsync(chat, fileId);

        Assert.Equal(HttpStatusCode.OK, (await chat.DeleteAsync(messageId)).StatusCode);
        var response = await ViewTab(app).GetAsync(new Uri(url).PathAndQuery, Cancel);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [Trait("Rule", "SEC-16")]
    [InlineData("/v/not-a-token")]
    [InlineData("/v/")]
    [InlineData("/api/me")]
    [InlineData("/api/settings")]
    [InlineData("/")]
    [InlineData("/main.js")]
    public async Task SEC16_view_host_answers_nothing_else(string path)
    {
        await using var app = await StartAsync(postgres);

        var response = await ViewTab(app).GetAsync(path, Cancel);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.Empty(await app.AccessLogAsync());
    }

    [Fact]
    [Trait("Rule", "SEC-16")]
    public async Task SEC16_link_works_only_on_the_view_host_and_only_with_get()
    {
        await using var app = await StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var (_, fileId) = await BotSendsAsync(chat, "report.html", "text/html");
        var path = new Uri((await LinkAsync(chat, fileId)).Url).PathAndQuery;

        var onApp = await chat.Browser.GetAsync(path, Cancel);
        var post = await ViewTab(app).PostAsync(path, null, Cancel);

        Assert.NotEqual(Report, await onApp.Content.ReadAsStringAsync(Cancel));
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
    }

    [Fact]
    [Trait("Rule", "SEC-16")]
    public async Task SEC16_token_of_one_file_does_not_open_another()
    {
        await using var app = await StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var (_, first) = await BotSendsAsync(chat, "a.html", "text/html", "first");
        await BotSendsAsync(chat, "b.html", "text/html", "second");
        var path = new Uri((await LinkAsync(chat, first)).Url).PathAndQuery;

        Assert.Equal("first", await (await ViewTab(app).GetAsync(path, Cancel)).Content.ReadAsStringAsync(Cancel));
        Assert.Equal(HttpStatusCode.NotFound, (await ViewTab(app).GetAsync(path + "x", Cancel)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "SEC-16")]
    public async Task SEC16_server_refuses_a_view_host_under_dalans_domain()
    {
        await using var app = RelayApp.Create(await postgres.NewDatabaseAsync(),
            ("Admin:SiteUrl", "https://dalanet.ir"), ("Relay:FileViewHost", "view.dalanet.ir"));

        var error = Assert.ThrowsAny<Exception>(() => app.CreateClient());

        Assert.Contains("Relay:FileViewHost", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "SEC-16")]
    public async Task SEC16_admin_panel_cannot_move_the_view_host()
    {
        await using var app = await StartAsync(postgres, ("Identity:AdminPhones", "09129990001"));
        var (admin, _) = await app.LoginAsync("Owner", "09129990001");

        var settings = await admin.JsonAsync("/api/admin/settings");
        var keys = settings.GetProperty("settings").EnumerateArray().Select(s => s.GetProperty("key").GetString()).ToList();

        Assert.Contains("Relay:FileViewMinutes", keys);
        Assert.DoesNotContain("Relay:FileViewHost", keys);
    }
}
