using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Testing;

namespace Dalan.Relay.Tests;

/// <summary>A voice and the length of audio files (REL-28, REL-50).</summary>
public class VoiceTests(PostgresServer postgres)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("Rule", "REL-28")]
    [Trait("Rule", "REL-50")]
    public async Task REL28_voice_with_a_duration_reaches_the_bot_and_the_history()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var first = await chat.Bot.UpdatesAsync();
        await chat.Bot.UpdatesAsync($"timeout=0&offset={first[^1].Id() + 1}");

        var response = await chat.UploadAsync([1, 2, 3], "audio/ogg", "voice.ogg", voiceDuration: "7");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var file = Assert.Single(await chat.Bot.UpdatesAsync()).GetProperty("message").GetProperty("file");
        Assert.Equal(("voice", "audio/ogg", 7), (file.GetProperty("kind").GetString(), file.GetProperty("type").GetString(), file.GetProperty("duration").GetInt32()));
        var shown = (await chat.Browser.HistoryAsync(chat.BotId))[^1].GetProperty("file");
        Assert.Equal(("voice", 7), (shown.GetProperty("kind").GetString(), shown.GetProperty("duration").GetInt32()));
    }

    [Fact]
    [Trait("Rule", "REL-28")]
    public async Task REL28_voice_header_makes_any_audio_type_a_voice_and_zero_is_fine()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);

        var file = (await (await chat.UploadAsync([1], "audio/webm", "v.webm", voiceDuration: "0")).Content.ReadFromJsonAsync<JsonElement>(Cancel)).GetProperty("file");

        Assert.Equal("voice", file.GetProperty("kind").GetString());
        Assert.Equal(0, file.GetProperty("duration").GetInt32());
    }

    [Fact]
    [Trait("Rule", "REL-28")]
    public async Task REL28_audio_without_a_duration_header_is_audio()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);

        var file = (await (await chat.UploadAsync([1], "audio/mpeg", "a.mp3")).Content.ReadFromJsonAsync<JsonElement>(Cancel)).GetProperty("file");

        Assert.Equal("audio", file.GetProperty("kind").GetString());
        Assert.False(file.TryGetProperty("duration", out _));
    }

    [Theory]
    [Trait("Rule", "REL-28")]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("1.5")]
    public async Task REL28_bad_duration_header_is_400_and_nothing_is_stored(string duration)
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);

        var response = await chat.UploadAsync([1], "audio/ogg", "v.ogg", voiceDuration: duration);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_request", await response.ErrorCodeAsync());
        Assert.Single(await app.MessagesAsync());
    }

    [Theory]
    [Trait("Rule", "REL-50")]
    [InlineData("voice", "audio/ogg")]
    [InlineData("audio", "audio/mpeg")]
    [InlineData("video", "video/mp4")]
    public async Task REL50_bot_sends_a_file_with_its_length(string kind, string type)
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);

        var answer = await (await chat.BotUploadAsync([1, 2], "f.bin", kind, type: type, duration: 12)).Content.ReadFromJsonAsync<JsonElement>(Cancel);

        Assert.Equal(12, answer.GetProperty("file").GetProperty("duration").GetInt32());
        Assert.Equal(12, (await chat.Browser.HistoryAsync(chat.BotId))[^1].GetProperty("file").GetProperty("duration").GetInt32());
    }

    [Fact]
    [Trait("Rule", "REL-50")]
    public async Task REL50_bot_sends_a_file_again_with_a_new_length_or_keeps_the_old_one()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var sent = await (await chat.BotUploadAsync([1, 2], "v.ogg", "voice", type: "audio/ogg", duration: 5)).Content.ReadFromJsonAsync<JsonElement>(Cancel);
        var fileId = sent.GetProperty("file").GetProperty("id").GetString();

        var same = await (await chat.Bot.PostAsJsonAsync("/v1/messages", new { user_id = chat.UserId, file_id = fileId, kind = "voice" }, Cancel)).Content.ReadFromJsonAsync<JsonElement>(Cancel);
        var changed = await (await chat.Bot.PostAsJsonAsync("/v1/messages", new { user_id = chat.UserId, file_id = fileId, kind = "voice", duration = 9 }, Cancel)).Content.ReadFromJsonAsync<JsonElement>(Cancel);

        Assert.Equal(5, same.GetProperty("file").GetProperty("duration").GetInt32());
        Assert.Equal(9, changed.GetProperty("file").GetProperty("duration").GetInt32());
    }

    [Fact]
    [Trait("Rule", "REL-50")]
    public async Task REL50_a_file_with_no_length_has_no_duration_field()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);

        var answer = await (await chat.BotUploadAsync([1, 2], "a.pdf", "document")).Content.ReadFromJsonAsync<JsonElement>(Cancel);

        Assert.False(answer.GetProperty("file").TryGetProperty("duration", out _));
    }
}
