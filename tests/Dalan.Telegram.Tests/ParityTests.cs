using System.Net.Http.Headers;
using System.Text.Json;
using Dalan.Relay.Chats;
using Dalan.Relay.Tests;
using Dalan.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Telegram.Tests;

/// <summary>Button styles and keyboard options, file lengths and the bot's profile photo (TG-08, TG-14, TG-20, TG-21, TG-23).</summary>
public class ParityTests(PostgresServer postgres)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];

    private async Task<(DalanApp App, TelegramBot Bot, long BotId, long User)> StartedAsync(params (string Key, string Value)[] settings)
    {
        var (app, bot, botId, _) = await TelegramBot.StartAsync(postgres, settings);
        var user = await app.AddUserAsync("Sara", "sara");
        await app.UserSaysAsync(botId, user, "/start");
        return (app, bot, botId, user);
    }

    private static JsonElement Json(string text) => JsonSerializer.Deserialize<JsonElement>(text);

    [Fact]
    [Trait("Rule", "TG-08")]
    public async Task TG08_style_and_options_are_kept()
    {
        var (app, bot, _, user) = await StartedAsync();
        await using var _1 = app;
        var markup = Json("""
            {"keyboard":[["a1","a2","a3","a4","a5"],["b1","b2","b3","b4"],["c1","c2",{"text":"➕","style":"success"}]],
             "is_persistent":true,"one_time_keyboard":true}
            """);

        (await bot.PostJsonAsync("sendMessage", new { chat_id = user, text = "menu", reply_markup = markup })).IsOk();

        var keyboard = (await app.MessagesAsync())[^1].Content.ReplyKeyboard!;
        Assert.Equal([5, 4, 3], keyboard.ToRows().Select(row => row.Count));
        var options = keyboard.ToOptions()!;
        Assert.Equal((true, false, true), (options.Persistent, options.Resize, options.OneTime));
        Assert.Equal("success", options.Styles![2][2]);
        Assert.Null(options.Styles[2][1]);
    }

    [Fact]
    [Trait("Rule", "TG-08")]
    public async Task TG08_look_only_fields_are_ignored()
    {
        var (app, bot, _, user) = await StartedAsync();
        await using var _1 = app;
        var markup = Json("""
            {"keyboard":[[{"text":"Yes","icon_custom_emoji_id":"5"},"No"]],"input_field_placeholder":"Pick","selective":true}
            """);

        (await bot.PostJsonAsync("sendMessage", new { chat_id = user, text = "Sure?", reply_markup = markup })).IsOk();

        Assert.Equal([["Yes", "No"]], (await app.MessagesAsync())[^1].Content.ReplyKeyboard!.ToRows());
        Assert.Null((await app.MessagesAsync())[^1].Content.ReplyKeyboard!.ToOptions());
    }

    [Fact]
    [Trait("Rule", "TG-08")]
    public async Task TG08_unknown_style_is_400_and_nothing_is_stored()
    {
        var (app, bot, _, user) = await StartedAsync();
        await using var _1 = app;
        var markup = Json("""{"keyboard":[[{"text":"x","style":"pink"}]]}""");

        (await bot.PostJsonAsync("sendMessage", new { chat_id = user, text = "x", reply_markup = markup })).IsError(400, "Bad Request");

        Assert.Single(await app.MessagesAsync());
    }

    [Fact]
    [Trait("Rule", "TG-14")]
    public async Task TG14_inline_style_is_kept_in_the_returned_message_and_the_history()
    {
        var (app, bot, _, user) = await StartedAsync();
        await using var _1 = app;
        var markup = Json("""{"inline_keyboard":[[{"text":"🗑","callback_data":"d","style":"danger"},{"text":"Site","url":"https://dalanet.ir"}]]}""");

        var answer = await bot.PostJsonAsync("sendMessage", new { chat_id = user, text = "pick", reply_markup = markup });

        answer.IsOk();
        var row = answer.Result.GetProperty("reply_markup").GetProperty("inline_keyboard")[0];
        Assert.Equal("danger", row[0].GetProperty("style").GetString());
        Assert.False(row[1].TryGetProperty("style", out _));
        Assert.Equal("danger", (await app.MessagesAsync())[^1].Content.InlineKeyboard!.ToRows()[0][0].Style);
    }

    [Fact]
    [Trait("Rule", "TG-14")]
    public async Task TG14_unknown_inline_style_is_400()
    {
        var (app, bot, _, user) = await StartedAsync();
        await using var _1 = app;
        var markup = Json("""{"inline_keyboard":[[{"text":"x","callback_data":"d","style":"pink"}]]}""");

        (await bot.PostJsonAsync("sendMessage", new { chat_id = user, text = "x", reply_markup = markup })).IsError(400, "Bad Request");

        Assert.Single(await app.MessagesAsync());
    }

    private static MultipartFormDataContent VoiceForm(long user, string? duration)
    {
        var form = new MultipartFormDataContent { { new StringContent(user.ToString(System.Globalization.CultureInfo.InvariantCulture)), "chat_id" } };
        if (duration is not null) form.Add(new StringContent(duration), "duration");
        var file = new ByteArrayContent([1, 2, 3]);
        file.Headers.ContentType = new MediaTypeHeaderValue("audio/ogg");
        form.Add(file, "voice", "v.ogg");
        return form;
    }

    [Fact]
    [Trait("Rule", "TG-20")]
    public async Task TG20_voice_keeps_its_length()
    {
        var (app, bot, _, user) = await StartedAsync();
        await using var _1 = app;
        using var form = VoiceForm(user, "5");

        var answer = await bot.PostAsync("sendVoice", form);

        answer.IsOk();
        var voice = answer.Result.GetProperty("voice");
        Assert.Equal(5, voice.GetProperty("duration").GetInt32());
        Assert.Equal("audio/ogg", voice.GetProperty("mime_type").GetString());
        Assert.Equal(5, (await app.MessagesAsync())[^1].Content.File!.Duration);
    }

    [Fact]
    [Trait("Rule", "TG-20")]
    public async Task TG20_voice_without_a_length_says_0()
    {
        var (app, bot, _, user) = await StartedAsync();
        await using var _1 = app;
        using var form = VoiceForm(user, null);

        var answer = await bot.PostAsync("sendVoice", form);

        Assert.Equal(0, answer.Result.GetProperty("voice").GetProperty("duration").GetInt32());
        Assert.Null((await app.MessagesAsync())[^1].Content.File!.Duration);
    }

    [Fact]
    [Trait("Rule", "TG-21")]
    public async Task TG21_user_voice_arrives_with_its_length()
    {
        var (app, bot, botId, _) = await TelegramBot.StartAsync(postgres);
        await using var _1 = app;
        var sara = await app.AddUserAsync("Sara", "sara");
        var (error, sent) = await app.Services.GetRequiredService<ChatService>().ReceiveFromUserAsync(
            botId, sara, "", new UserUpload(new MemoryStream([1, 2, 3]), "voice.ogg", "audio/ogg", VoiceSeconds: 7), Cancel);
        Assert.Equal(ChatError.None, error);
        Assert.NotNull(sent);

        var answer = await bot.GetAsync("getUpdates", "timeout=0");

        var fileId = answer.Result[0].GetProperty("message").GetProperty("voice").GetProperty("file_id").GetString();
        answer.ResultEquals("userVoice.json", fileId);
        var file = await bot.PostJsonAsync("getFile", new { file_id = fileId });
        Assert.Equal(3, file.Result.GetProperty("file_size").GetInt64());
    }

    private static MultipartFormDataContent PhotoForm(string photoJson, byte[] bytes, string partName = "p")
    {
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return new MultipartFormDataContent { { new StringContent(photoJson), "photo" }, { part, partName, "me.jpg" } };
    }

    [Fact]
    [Trait("Rule", "TG-23")]
    public async Task TG23_set_and_remove_the_profile_photo()
    {
        var (app, bot, botId, _) = await StartedAsync();
        await using var _1 = app;
        using var form = PhotoForm("""{"type":"static","photo":"attach://p"}""", Jpeg);

        var set = await bot.PostAsync("setMyProfilePhoto", form);

        Assert.True(set.Result.GetBoolean());
        var read = await app.CreateClient().GetAsync($"/api/bots/{botId}/icon", Cancel);
        Assert.Equal("image/jpeg", read.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Jpeg, await read.Content.ReadAsByteArrayAsync(Cancel));

        var removed = await bot.PostJsonAsync("removeMyProfilePhoto", new { });

        Assert.True(removed.Result.GetBoolean());
        Assert.Equal(System.Net.HttpStatusCode.NotFound, (await app.CreateClient().GetAsync($"/api/bots/{botId}/icon", Cancel)).StatusCode);
    }

    [Theory]
    [Trait("Rule", "TG-23")]
    [InlineData("""{"type":"animated","animation":"attach://p"}""", "Bad Request: animated profile photos are not supported by Dalan")]
    [InlineData("""{"type":"funny","photo":"attach://p"}""", "Bad Request: IMAGE_PROCESS_FAILED")]
    [InlineData("""{"type":"static","photo":"attach://missing"}""", "Bad Request: IMAGE_PROCESS_FAILED")]
    public async Task TG23_bad_input_is_400(string photoJson, string description)
    {
        var (app, bot, botId, _) = await StartedAsync();
        await using var _1 = app;
        using var form = PhotoForm(photoJson, Jpeg);

        (await bot.PostAsync("setMyProfilePhoto", form)).IsError(400, description);

        Assert.Equal(System.Net.HttpStatusCode.NotFound, (await app.CreateClient().GetAsync($"/api/bots/{botId}/icon", Cancel)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "TG-23")]
    public async Task TG23_text_file_is_refused_and_the_old_icon_stays()
    {
        var (app, bot, botId, _) = await StartedAsync();
        await using var _1 = app;
        using var good = PhotoForm("""{"type":"static","photo":"attach://p"}""", Jpeg);
        (await bot.PostAsync("setMyProfilePhoto", good)).IsOk();
        using var text = PhotoForm("""{"type":"static","photo":"attach://p"}""", "just text"u8.ToArray());

        (await bot.PostAsync("setMyProfilePhoto", text)).IsError(400, "Bad Request: IMAGE_PROCESS_FAILED");

        Assert.Equal(Jpeg, await app.CreateClient().GetByteArrayAsync($"/api/bots/{botId}/icon", Cancel));
    }

    [Fact]
    [Trait("Rule", "TG-23")]
    public async Task TG23_photo_over_the_limit_is_400()
    {
        var (app, bot, _, _) = await StartedAsync(("Relay:MaxBotIconKb", "1"));
        await using var _1 = app;
        using var form = PhotoForm("""{"type":"static","photo":"attach://p"}""", [.. Jpeg, .. new byte[2048]]);

        (await bot.PostAsync("setMyProfilePhoto", form)).IsError(400, "Bad Request: photo is too big");
    }
}
