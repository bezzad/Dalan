using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Relay.Buttons;
using Dalan.Relay.Chats;
using Dalan.Relay.Live;
using Dalan.Relay.Tests;
using Dalan.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Telegram.Tests;

/// <summary>Inline buttons, edit, delete, pin, commands and files through the adapter (TG-14 to TG-21).</summary>
public class RichTests(PostgresServer postgres)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static readonly object InlineYes = new { inline_keyboard = new[] { new[] { new { text = "Yes", callback_data = "y" } } } };

    private async Task<(DalanApp App, TelegramBot Bot, long BotId, long User)> StartedAsync()
    {
        var (app, bot, botId, _) = await TelegramBot.StartAsync(postgres);
        var user = await app.AddUserAsync("Sara", "sara");
        await app.UserSaysAsync(botId, user, "/start");
        return (app, bot, botId, user);
    }

    private static async Task<long> SendAsync(TelegramBot bot, long user, string text, object? markup = null)
    {
        var answer = await bot.PostJsonAsync("sendMessage", new { chat_id = user, text, reply_markup = markup });
        answer.IsOk();
        return answer.Result.GetProperty("message_id").GetInt64();
    }

    private static async Task<string> PressAsync(DalanApp app, long botId, long user, long messageId) =>
        (await app.Services.GetRequiredService<ButtonPresses>().PressAsync(botId, user, messageId, "y", Cancel))!;

    [Fact]
    [Trait("Rule", "TG-14")]
    public async Task TG14_press_reaches_get_updates_as_a_callback_query()
    {
        var (app, bot, botId, user) = await StartedAsync();
        await using var _1 = app;
        var id = await SendAsync(bot, user, "pick", InlineYes);
        await bot.UpdatesAsync($"timeout=0&offset={(await bot.UpdatesAsync())[^1].GetProperty("update_id").GetInt64() + 1}");

        await PressAsync(app, botId, user, id);

        (await bot.GetAsync("getUpdates", "timeout=0")).ResultEquals("callbackQuery.json");
    }

    [Fact]
    [Trait("Rule", "TG-14")]
    public async Task TG14_sent_message_carries_its_inline_keyboard()
    {
        var (app, bot, _, user) = await StartedAsync();
        await using var _1 = app;

        var answer = await bot.PostJsonAsync("sendMessage", new { chat_id = user, text = "pick", reply_markup = InlineYes });

        answer.IsOk();
        Assert.Equal("y", answer.Result.GetProperty("reply_markup").GetProperty("inline_keyboard")[0][0].GetProperty("callback_data").GetString());
    }

    [Theory]
    [Trait("Rule", "TG-14")]
    [InlineData("switch_inline_query")]
    [InlineData("web_app")]
    [InlineData("login_url")]
    public async Task TG14_inline_button_kind_dalan_does_not_have_is_400(string field)
    {
        var (app, bot, _, user) = await StartedAsync();
        await using var _1 = app;
        var markup = JsonSerializer.Deserialize<JsonElement>($$"""{"inline_keyboard":[[{"text":"A","{{field}}":"x"}]]}""");

        (await bot.PostJsonAsync("sendMessage", new { chat_id = user, text = "x", reply_markup = markup })).IsError(400, "Bad Request");
        Assert.Single(await app.MessagesAsync());
    }

    [Fact]
    [Trait("Rule", "TG-15")]
    public async Task TG15_answer_reaches_the_user()
    {
        var (app, bot, botId, user) = await StartedAsync();
        await using var _1 = app;
        var id = await SendAsync(bot, user, "pick", InlineYes);
        var pressId = await PressAsync(app, botId, user, id);
        using var stream = app.Services.GetRequiredService<IMessageNotifier>().Subscribe(user);

        var answer = await bot.PostJsonAsync("answerCallbackQuery", new { callback_query_id = pressId, text = "Saved" });

        answer.IsOk();
        Assert.True(answer.Result.GetBoolean());
        // The "not delivered" mark of the message the press replaced (REL-31) may come first.
        LiveEvent? heard;
        while (stream.Events.TryRead(out heard) && heard is not CallbackAnswered)
        {
        }

        Assert.Equal("Saved", Assert.IsType<CallbackAnswered>(heard).Text);
    }

    [Fact]
    [Trait("Rule", "TG-15")]
    public async Task TG15_unknown_id_or_url_is_400()
    {
        var (app, bot, botId, user) = await StartedAsync();
        await using var _1 = app;
        var pressId = await PressAsync(app, botId, user, await SendAsync(bot, user, "pick", InlineYes));

        (await bot.PostJsonAsync("answerCallbackQuery", new { callback_query_id = "999" }))
            .IsError(400, "Bad Request: query is too old");
        (await bot.PostJsonAsync("answerCallbackQuery", new { callback_query_id = pressId, url = "https://t.me/x" }))
            .IsError(400, "Bad Request");
    }

    [Fact]
    [Trait("Rule", "TG-16")]
    public async Task TG16_edit_text_returns_the_message_with_an_edit_date()
    {
        var (app, bot, _, user) = await StartedAsync();
        await using var _1 = app;
        var id = await SendAsync(bot, user, "wait", InlineYes);

        var answer = await bot.PostJsonAsync("editMessageText", new { chat_id = user, message_id = id, text = "done" });

        answer.IsOk();
        Assert.Equal("done", answer.Result.GetProperty("text").GetString());
        Assert.Equal(app.Time.GetUtcNow().ToUnixTimeSeconds(), answer.Result.GetProperty("edit_date").GetInt64());
        Assert.False(answer.Result.TryGetProperty("reply_markup", out _));
    }

    [Fact]
    [Trait("Rule", "TG-16")]
    public async Task TG16_edit_markup_keeps_the_text()
    {
        var (app, bot, _, user) = await StartedAsync();
        await using var _1 = app;
        var id = await SendAsync(bot, user, "pick");

        var answer = await bot.PostJsonAsync("editMessageReplyMarkup", new { chat_id = user, message_id = id, reply_markup = InlineYes });

        answer.IsOk();
        Assert.Equal("pick", answer.Result.GetProperty("text").GetString());
        Assert.True(answer.Result.TryGetProperty("reply_markup", out _));
    }

    [Fact]
    [Trait("Rule", "TG-16")]
    public async Task TG16_edit_errors_use_telegram_texts()
    {
        var (app, bot, _, user) = await StartedAsync();
        await using var _1 = app;
        var id = await SendAsync(bot, user, "same");

        (await bot.PostJsonAsync("editMessageText", new { chat_id = user, message_id = id, text = "same" }))
            .IsError(400, "Bad Request: message is not modified");
        (await bot.PostJsonAsync("editMessageText", new { chat_id = user, message_id = 99, text = "x" }))
            .IsError(400, "Bad Request: message to edit not found");
        (await bot.PostJsonAsync("editMessageText", new { chat_id = user, message_id = 1, text = "x" }))
            .IsError(400, "Bad Request: message can't be edited");
        (await bot.PostJsonAsync("editMessageText", new { inline_message_id = "abc", text = "x" }))
            .IsError(400, "Bad Request");
    }

    [Fact]
    [Trait("Rule", "TG-17")]
    public async Task TG17_delete_message()
    {
        var (app, bot, _, user) = await StartedAsync();
        await using var _1 = app;
        var id = await SendAsync(bot, user, "oops");

        var answer = await bot.PostJsonAsync("deleteMessage", new { chat_id = user, message_id = id });

        answer.IsOk();
        Assert.True(answer.Result.GetBoolean());
        Assert.True((await app.MessagesAsync())[^1].Deleted);
        (await bot.PostJsonAsync("deleteMessage", new { chat_id = user, message_id = id }))
            .IsError(400, "Bad Request: message to delete not found");
    }

    [Fact]
    [Trait("Rule", "TG-17")]
    public async Task TG17_delete_several_messages_skips_missing_ones()
    {
        var (app, bot, _, user) = await StartedAsync();
        await using var _1 = app;
        var first = await SendAsync(bot, user, "one");
        var second = await SendAsync(bot, user, "two");
        var kept = await SendAsync(bot, user, "three");

        var answer = await bot.PostJsonAsync("deleteMessages", new { chat_id = user, message_ids = new[] { first, second, 99 } });

        answer.IsOk();
        Assert.True(answer.Result.GetBoolean());
        var messages = await app.MessagesAsync();
        Assert.All(messages.Where(m => m.Seq == first || m.Seq == second), m => Assert.True(m.Deleted));
        Assert.False(messages.Single(m => m.Seq == kept).Deleted);
    }

    [Theory]
    [Trait("Rule", "TG-17")]
    [InlineData("[]")]
    [InlineData("[\"a\"]")]
    [InlineData("null")]
    public async Task TG17_delete_messages_with_a_bad_list_is_a_400(string ids)
    {
        var (app, bot, _, user) = await StartedAsync();
        await using var _1 = app;
        var id = await SendAsync(bot, user, "keep");

        var answer = await bot.PostJsonAsync("deleteMessages",
            JsonElement.Parse($"{{\"chat_id\":{user},\"message_ids\":{ids}}}"));

        answer.IsError(400, "Bad Request");
        Assert.False((await app.MessagesAsync()).Single(m => m.Seq == id).Deleted);
    }

    [Fact]
    [Trait("Rule", "TG-17")]
    public async Task TG17_delete_messages_with_more_than_100_ids_is_a_400()
    {
        var (app, bot, _, user) = await StartedAsync();
        await using var _1 = app;
        var id = await SendAsync(bot, user, "keep");

        var answer = await bot.PostJsonAsync("deleteMessages",
            new { chat_id = user, message_ids = Enumerable.Repeat(id, 101).ToArray() });

        answer.IsError(400, "Bad Request");
        Assert.False((await app.MessagesAsync()).Single(m => m.Seq == id).Deleted);
    }

    [Fact]
    [Trait("Rule", "TG-18")]
    public async Task TG18_pin_and_unpin_all()
    {
        var (app, bot, _, user) = await StartedAsync();
        await using var _1 = app;
        var id = await SendAsync(bot, user, "rules");

        (await bot.PostJsonAsync("pinChatMessage", new { chat_id = user, message_id = id })).IsOk();
        Assert.True((await app.MessagesAsync())[^1].Pinned);

        (await bot.PostJsonAsync("unpinAllChatMessages", new { chat_id = user })).IsOk();
        Assert.DoesNotContain(await app.MessagesAsync(), m => m.Pinned);
        (await bot.PostJsonAsync("pinChatMessage", new { chat_id = user, message_id = 99 }))
            .IsError(400, "Bad Request: message to pin not found");
    }

    [Fact]
    [Trait("Rule", "TG-19")]
    public async Task TG19_set_get_and_delete_commands()
    {
        var (app, bot, _, _) = await StartedAsync();
        await using var _1 = app;

        (await bot.PostJsonAsync("setMyCommands", new { commands = new[] { new { command = "start", description = "Start" } } })).IsOk();
        var read = await bot.PostJsonAsync("getMyCommands", new { });
        (await bot.PostJsonAsync("deleteMyCommands", new { })).IsOk();
        var afterDelete = await bot.PostJsonAsync("getMyCommands", new { });

        Assert.Equal("""[{"command":"start","description":"Start"}]""", read.Result.GetRawText());
        Assert.Equal(0, afterDelete.Result.GetArrayLength());
    }

    [Fact]
    [Trait("Rule", "TG-19")]
    public async Task TG19_list_for_another_language_or_scope_changes_nothing()
    {
        var (app, bot, _, _) = await StartedAsync();
        await using var _1 = app;
        (await bot.PostJsonAsync("setMyCommands", new { commands = new[] { new { command = "start", description = "Start" } } })).IsOk();

        var english = await bot.PostJsonAsync("setMyCommands",
            new { commands = new[] { new { command = "other", description = "Other" } }, language_code = "en" });
        var group = await bot.PostJsonAsync("setMyCommands",
            new { commands = new[] { new { command = "other", description = "Other" } }, scope = new { type = "all_group_chats" } });

        english.IsOk();
        group.IsOk();
        var read = await bot.PostJsonAsync("getMyCommands", new { });
        Assert.Equal("start", read.Result[0].GetProperty("command").GetString());
        Assert.Equal(0, (await bot.PostJsonAsync("getMyCommands", new { language_code = "en" })).Result.GetArrayLength());
    }

    private static MultipartFormDataContent PhotoForm(long user, string caption = "cat")
    {
        var photo = new ByteArrayContent([1, 2, 3]);
        photo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return new MultipartFormDataContent
        {
            { new StringContent(user.ToString(System.Globalization.CultureInfo.InvariantCulture)), "chat_id" },
            { new StringContent(caption), "caption" },
            { photo, "photo", "cat.jpg" },
        };
    }

    [Fact]
    [Trait("Rule", "TG-20")]
    public async Task TG20_send_photo_by_upload()
    {
        var (app, bot, _, user) = await StartedAsync();
        await using var _1 = app;
        using var form = PhotoForm(user);

        var answer = await bot.PostAsync("sendPhoto", form);

        var fileId = answer.Result.GetProperty("photo")[0].GetProperty("file_id").GetString();
        answer.ResultEquals("sentPhoto.json", fileId);
        var file = (await app.MessagesAsync())[^1].Content.File!;
        Assert.Equal(("cat.jpg", "image/jpeg"), (file.Name, file.ContentType));
    }

    [Theory]
    [Trait("Rule", "TG-20")]
    [InlineData("sendDocument", "document")]
    [InlineData("sendAudio", "audio")]
    [InlineData("sendVoice", "voice")]
    [InlineData("sendVideo", "video")]
    public async Task TG20_send_a_file_again_by_its_id(string method, string field)
    {
        var (app, bot, _, user) = await StartedAsync();
        await using var _1 = app;
        using var form = PhotoForm(user);
        var fileId = (await bot.PostAsync("sendPhoto", form)).Result.GetProperty("photo")[0].GetProperty("file_id").GetString();

        var answer = await bot.PostJsonAsync(method, new Dictionary<string, object> { ["chat_id"] = user, [field] = fileId! });

        answer.IsOk();
        Assert.Equal(fileId, answer.Result.GetProperty(field).GetProperty("file_id").GetString());
    }

    [Fact]
    [Trait("Rule", "TG-20")]
    public async Task TG20_url_or_unknown_id_is_400()
    {
        var (app, bot, _, user) = await StartedAsync();
        await using var _1 = app;

        (await bot.PostJsonAsync("sendDocument", new { chat_id = user, document = "https://example.com/a.pdf" })).IsError(400, "Bad Request");
        (await bot.PostJsonAsync("sendDocument", new { chat_id = user, document = "AAAAAAAAAAAAAAAAAAAAAA" }))
            .IsError(400, "Bad Request: wrong file identifier");
        Assert.Single(await app.MessagesAsync());
    }

    [Fact]
    [Trait("Rule", "TG-21")]
    public async Task TG21_user_photo_arrives_and_downloads()
    {
        var (app, bot, botId, user) = await StartedAsync();
        await using var _1 = app;
        await bot.UpdatesAsync($"timeout=0&offset={(await bot.UpdatesAsync())[^1].GetProperty("update_id").GetInt64() + 1}");
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 9];
        var (_, sent) = await app.Services.GetRequiredService<ChatService>().ReceiveFromUserAsync(
            botId, user, "look", new UserUpload(new MemoryStream(png), "a.png", "image/png"), Cancel);
        Assert.NotNull(sent);

        var message = Assert.Single(await bot.UpdatesAsync()).GetProperty("message");
        Assert.Equal("look", message.GetProperty("caption").GetString());
        Assert.False(message.TryGetProperty("text", out _));
        var fileId = message.GetProperty("photo")[0].GetProperty("file_id").GetString();

        var file = await bot.PostJsonAsync("getFile", new { file_id = fileId });
        file.IsOk();
        Assert.Equal(5, file.Result.GetProperty("file_size").GetInt64());
        var path = file.Result.GetProperty("file_path").GetString();
        var download = await app.CreateClient().GetAsync($"/file/bot{BotToken(bot)}/{path}", Cancel);
        Assert.Equal(png, await download.Content.ReadAsByteArrayAsync(Cancel));
        (await bot.PostJsonAsync("getFile", new { file_id = "nope" })).IsError(400, "Bad Request: invalid file_id");
    }

    /// <summary>The token is in the bot's method address: "/bot&lt;token&gt;/x?".</summary>
    private static string BotToken(TelegramBot bot) => bot.Url("x")["/bot".Length..^"/x?".Length];
}
