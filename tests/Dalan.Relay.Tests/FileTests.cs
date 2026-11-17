using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Relay.Files;
using Dalan.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Relay.Tests;

/// <summary>Files from bots and users, downloads and expiry (REL-07, REL-18, REL-27 to REL-30, SEC-05, SEC-12).</summary>
public class FileTests(PostgresServer postgres)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    /// <summary>The test moved the clock past the session's end: the client renews it, as the real one does (CLI-11).</summary>
    private static async Task RenewSessionAsync(HttpClient browser) =>
        (await browser.PostAsync("/api/auth/refresh", null, Cancel)).EnsureSuccessStatusCode();

    private static async Task<JsonElement> FileOfAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Cancel)).GetProperty("file");
    }

    [Fact]
    [Trait("Rule", "REL-27")]
    public async Task REL27_bot_sends_a_photo_with_a_caption()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);

        var file = await FileOfAsync(await chat.BotUploadAsync([1, 2, 3], "cat.jpg", "photo", "cat"));

        var message = (await chat.Browser.HistoryAsync(chat.BotId))[^1];
        Assert.Equal("cat", message.GetProperty("text").GetString());
        var shown = message.GetProperty("file");
        Assert.Equal(file.GetProperty("id").GetString(), shown.GetProperty("id").GetString());
        Assert.Equal("photo", shown.GetProperty("kind").GetString());
        Assert.Equal("cat.jpg", shown.GetProperty("name").GetString());
        Assert.Equal("image/jpeg", shown.GetProperty("type").GetString());
        Assert.Equal(3, shown.GetProperty("size").GetInt64());
    }

    [Fact]
    [Trait("Rule", "REL-27")]
    public async Task REL27_bot_sends_a_file_again_by_id_to_another_user()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var alice = await Chat.StartAsync(app);
        var (bob, bobId) = await app.LoginAsync("Bob");
        await bob.SayAsync(alice.BotId, "/start");
        var fileId = (await FileOfAsync(await alice.BotUploadAsync([7, 8, 9], "a.pdf", "document"))).GetProperty("id").GetString();

        var again = await alice.Bot.PostAsJsonAsync("/v1/messages", new { user_id = bobId, file_id = fileId, kind = "document", text = "" }, Cancel);

        Assert.Equal(fileId, (await FileOfAsync(again)).GetProperty("id").GetString());
        Assert.Equal([7, 8, 9], await bob.GetByteArrayAsync($"/api/bots/{alice.BotId}/files/{fileId}", Cancel));
    }

    [Fact]
    [Trait("Rule", "REL-27")]
    public async Task REL27_file_over_the_limit_is_413_and_nothing_is_stored()
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Relay:MaxFileSizeMb", "1"));
        var chat = await Chat.StartAsync(app);

        var response = await chat.BotUploadAsync(new byte[(1024 * 1024) + 1], "big.bin", "document");

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("file_too_big", await response.ErrorCodeAsync());
        Assert.Single(await app.MessagesAsync());
        Assert.Empty(Directory.Exists(app.FileFolder) ? Directory.GetFiles(app.FileFolder) : []);
    }

    [Fact]
    [Trait("Rule", "REL-27")]
    public async Task REL27_unknown_file_id_is_400()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);

        var response = await chat.Bot.PostAsJsonAsync("/v1/messages", new { user_id = chat.UserId, file_id = "AAAAAAAAAAAAAAAAAAAAAA" }, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("file_not_found", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "REL-28")]
    [Trait("Rule", "REL-07")]
    [Trait("Rule", "REL-29")]
    public async Task REL28_user_photo_reaches_the_bot_and_the_bot_downloads_it()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var first = await chat.Bot.UpdatesAsync();
        await chat.Bot.UpdatesAsync($"timeout=0&offset={first[^1].Id() + 1}");

        var response = await chat.UploadAsync(Png, "image/png", "شکل.png", "نگاه کن");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var message = Assert.Single(await chat.Bot.UpdatesAsync()).GetProperty("message");
        Assert.Equal("نگاه کن", message.GetProperty("text").GetString());
        var file = message.GetProperty("file");
        Assert.Equal("photo", file.GetProperty("kind").GetString());
        Assert.Equal("شکل.png", file.GetProperty("name").GetString());
        Assert.Equal(Png, await chat.Bot.GetByteArrayAsync($"/v1/files/{file.GetProperty("id").GetString()}", Cancel));
    }

    [Theory]
    [Trait("Rule", "REL-28")]
    [InlineData("audio/ogg", "audio")]
    [InlineData("video/mp4", "video")]
    [InlineData("application/pdf", "document")]
    [InlineData("image/svg+xml", "document")]
    public async Task REL28_kind_comes_from_the_type(string type, string kind)
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);

        Assert.Equal(kind, (await FileOfAsync(await chat.UploadAsync([1], type, "x"))).GetProperty("kind").GetString());
    }

    [Fact]
    [Trait("Rule", "REL-28")]
    [Trait("Rule", "SEC-05")]
    public async Task REL28_upload_without_a_name_or_as_a_form_is_400()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        using var form = new MultipartFormDataContent { { new ByteArrayContent([1]), "file", "a.txt" } };

        var noName = await chat.UploadAsync([1], "text/plain", null);
        var asForm = await chat.Browser.PostAsync($"/api/bots/{chat.BotId}/files", form, Cancel);

        Assert.Equal("bad_request", await noName.ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, asForm.StatusCode);
        Assert.Single(await app.MessagesAsync());
    }

    [Fact]
    [Trait("Rule", "REL-28")]
    public async Task REL28_daily_upload_limit()
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Relay:UserUploadMbPerDay", "1"));
        var chat = await Chat.StartAsync(app);
        Assert.Equal(HttpStatusCode.OK, (await chat.UploadAsync(new byte[1024 * 1024], "application/pdf", "a.pdf")).StatusCode);

        var over = await chat.UploadAsync([1], "application/pdf", "b.pdf");
        app.Time.Advance(TimeSpan.FromDays(1));
        await RenewSessionAsync(chat.Browser);
        var nextDay = await chat.UploadAsync([1], "application/pdf", "c.pdf");

        Assert.Equal(HttpStatusCode.TooManyRequests, over.StatusCode);
        Assert.Equal("upload_limit", await over.ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.OK, nextDay.StatusCode);
    }

    [Fact]
    [Trait("Rule", "REL-29")]
    public async Task REL29_another_user_cannot_get_the_file()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var alice = await Chat.StartAsync(app);
        var (bob, _) = await app.LoginAsync("Bob");
        await bob.SayAsync(alice.BotId, "/start");
        var fileId = (await FileOfAsync(await alice.UploadAsync([1, 2], "application/pdf", "a.pdf"))).GetProperty("id").GetString();

        var response = await bob.GetAsync($"/api/bots/{alice.BotId}/files/{fileId}", Cancel);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("file_not_found", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "REL-29")]
    public async Task REL29_another_bot_cannot_get_the_file()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var fileId = (await FileOfAsync(await chat.UploadAsync([1, 2], "application/pdf", "a.pdf"))).GetProperty("id").GetString();
        var (_, otherToken) = await app.AddBotAsync("other");

        var response = await app.BotClient(otherToken).GetAsync($"/v1/files/{fileId}", Cancel);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("Rule", "SEC-12")]
    public async Task SEC12_html_file_is_an_attachment_in_a_sandbox()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var fileId = (await FileOfAsync(await chat.BotUploadAsync("<script>alert(1)</script>"u8.ToArray(), "page.html", "document", type: "text/html")))
            .GetProperty("id").GetString();

        var response = await chat.Browser.GetAsync($"/api/bots/{chat.BotId}/files/{fileId}", Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("sandbox", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "SEC-12")]
    public async Task SEC12_photo_is_shown_inline()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var fileId = (await FileOfAsync(await chat.UploadAsync(Png, "image/png", "a.png"))).GetProperty("id").GetString();

        var response = await chat.Browser.GetAsync($"/api/bots/{chat.BotId}/files/{fileId}", Cancel);

        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("inline", response.Content.Headers.ContentDisposition?.DispositionType);
    }

    [Fact]
    [Trait("Rule", "REL-30")]
    public async Task REL30_old_file_is_swept_from_disk_and_table()
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Relay:FileRetentionDays", "30"));
        var chat = await Chat.StartAsync(app);
        var fileId = (await FileOfAsync(await chat.BotUploadAsync([1], "a.pdf", "document"))).GetProperty("id").GetString();
        var sweeper = app.Services.GetRequiredService<FileSweeper>();

        app.Time.Advance(TimeSpan.FromDays(29));
        await sweeper.SweepOnceAsync(Cancel);
        Assert.True(File.Exists(Path.Combine(app.FileFolder, fileId!)));

        app.Time.Advance(TimeSpan.FromDays(1));
        await sweeper.SweepOnceAsync(Cancel);

        Assert.False(File.Exists(Path.Combine(app.FileFolder, fileId!)));
        Assert.Equal(0, await PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT count(*) FROM relay.files"));
        Assert.Equal(HttpStatusCode.NotFound, (await chat.Bot.GetAsync($"/v1/files/{fileId}", Cancel)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "REL-30")]
    public async Task REL30_file_the_bot_never_read_is_swept_at_once()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var fileId = (await FileOfAsync(await chat.UploadAsync([1], "application/pdf", "a.pdf"))).GetProperty("id").GetString();

        app.Time.Advance(TimeSpan.FromSeconds(61));
        await app.Services.GetRequiredService<Dalan.Relay.Queue.ExpirySweeper>().SweepOnceAsync();
        await app.Services.GetRequiredService<FileSweeper>().SweepOnceAsync(Cancel);

        Assert.False(File.Exists(Path.Combine(app.FileFolder, fileId!)));
    }

    [Fact]
    [Trait("Rule", "REL-18")]
    public async Task REL18_settings_have_the_caption_and_file_limits()
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Relay:MaxCaptionLength", "11"), ("Relay:MaxFileSizeMb", "3"));
        var (browser, _) = await app.LoginAsync();

        var settings = await browser.JsonAsync("/api/settings");

        Assert.Equal(11, settings.GetProperty("max_caption_length").GetInt32());
        Assert.Equal(3, settings.GetProperty("max_file_size_mb").GetInt32());
    }
}
