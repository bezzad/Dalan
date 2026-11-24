using System.Net.Http.Headers;
using System.Text.Json;
using Dalan.Relay.Tests;
using Dalan.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Telegram.Tests;

/// <summary>Replies, formatting, switch buttons and caption edits through the adapter (TG-14, TG-16, TG-24, TG-25).</summary>
public class ReplyFormatTests(PostgresServer postgres)
{
    private async Task<(DalanApp App, TelegramBot Bot, long BotId, long User)> StartedAsync()
    {
        var (app, bot, botId, _) = await TelegramBot.StartAsync(postgres);
        var user = await app.AddUserAsync("Sara", "sara");
        await app.UserSaysAsync(botId, user, "/start");
        return (app, bot, botId, user);
    }

    private static JsonElement Json(string text) => JsonSerializer.Deserialize<JsonElement>(text);

    private static async Task<long> SendAsync(TelegramBot bot, object body)
    {
        var answer = await bot.PostJsonAsync("sendMessage", body);
        answer.IsOk();
        return answer.Result.GetProperty("message_id").GetInt64();
    }

    private static async Task<long> PhotoAsync(TelegramBot bot, long user, string caption)
    {
        var photo = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0, 1]);
        photo.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        using var form = new MultipartFormDataContent
        {
            { new StringContent(user.ToString(System.Globalization.CultureInfo.InvariantCulture)), "chat_id" },
            { new StringContent(caption), "caption" },
            { photo, "photo", "cat.jpg" },
        };
        var answer = await bot.PostAsync("sendPhoto", form);
        answer.IsOk();
        return answer.Result.GetProperty("message_id").GetInt64();
    }

    [Fact]
    [Trait("Rule", "TG-24")]
    public async Task TG24_user_reply_reaches_the_bot_with_reply_to_message()
    {
        var (app, bot, botId, user) = await StartedAsync();
        await using var _1 = app;
        var question = await SendAsync(bot, new { chat_id = user, text = "Which session?" });
        var offset = (await bot.UpdatesAsync())[^1].GetProperty("update_id").GetInt64() + 1;

        await app.Services.GetRequiredService<Relay.Chats.ChatService>()
            .ReceiveFromUserAsync(botId, user, "the second", null, TestContext.Current.CancellationToken, question);

        var message = (await bot.UpdatesAsync($"timeout=0&offset={offset}"))[0].GetProperty("message");
        var reply = message.GetProperty("reply_to_message");
        Assert.Equal(question, reply.GetProperty("message_id").GetInt64());
        Assert.True(reply.GetProperty("from").GetProperty("is_bot").GetBoolean());
        Assert.Equal("Which session?", reply.GetProperty("text").GetString());
        Assert.False(reply.TryGetProperty("reply_to_message", out _));
    }

    [Fact]
    [Trait("Rule", "TG-24")]
    public async Task TG24_bot_replies_with_reply_parameters_or_the_old_field()
    {
        var (app, bot, _, user) = await StartedAsync();
        await using var _1 = app;
        var userMessage = (await app.MessagesAsync())[^1].Seq;

        var answer = await bot.PostJsonAsync("sendMessage", new { chat_id = user, text = "hi", reply_parameters = new { message_id = userMessage } });
        var old = await bot.PostJsonAsync("sendMessage", new { chat_id = user, text = "hi", reply_to_message_id = userMessage });

        answer.IsOk();
        var reply = answer.Result.GetProperty("reply_to_message");
        Assert.Equal(userMessage, reply.GetProperty("message_id").GetInt64());
        Assert.False(reply.GetProperty("from").GetProperty("is_bot").GetBoolean());
        Assert.Equal("/start", reply.GetProperty("text").GetString());
        Assert.Equal(userMessage, old.Result.GetProperty("reply_to_message").GetProperty("message_id").GetInt64());
        Assert.Equal(userMessage, (await app.MessagesAsync())[^1].Content.ReplyTo!.Seq);
    }

    [Fact]
    [Trait("Rule", "TG-24")]
    public async Task TG24_missing_target_is_400_unless_sending_without_reply_is_allowed()
    {
        var (app, bot, _, user) = await StartedAsync();
        await using var _1 = app;

        var plain = await bot.PostJsonAsync("sendMessage", new { chat_id = user, text = "hi", reply_parameters = new { message_id = 99 } });
        var allowed = await bot.PostJsonAsync("sendMessage",
            new { chat_id = user, text = "hi", reply_parameters = new { message_id = 99, allow_sending_without_reply = true } });

        plain.IsError(400, "Bad Request: message to be replied not found");
        allowed.IsOk();
        Assert.False(allowed.Result.TryGetProperty("reply_to_message", out _));
    }

    [Fact]
    [Trait("Rule", "TG-25")]
    public async Task TG25_html_becomes_text_and_entities()
    {
        var (app, bot, _, user) = await StartedAsync();
        await using var _1 = app;

        var answer = await bot.PostJsonAsync("sendMessage",
            new { chat_id = user, text = """<b>Buy</b> &amp; <a href="https://dalanet.ir">see</a>""", parse_mode = "HTML" });

        answer.IsOk();
        Assert.Equal("Buy & see", answer.Result.GetProperty("text").GetString());
        var entities = answer.Result.GetProperty("entities");
        Assert.Equal(("bold", 0, 3), (entities[0].GetProperty("type").GetString(), entities[0].GetProperty("offset").GetInt32(), entities[0].GetProperty("length").GetInt32()));
        Assert.Equal(("text_link", 6, 3), (entities[1].GetProperty("type").GetString(), entities[1].GetProperty("offset").GetInt32(), entities[1].GetProperty("length").GetInt32()));
        Assert.Equal("https://dalanet.ir", entities[1].GetProperty("url").GetString());
        var stored = (await app.MessagesAsync())[^1].Content;
        Assert.Equal("Buy & see", stored.Text);
        Assert.Equal(2, stored.Entities!.Count);
    }

    [Fact]
    [Trait("Rule", "TG-25")]
    public async Task TG25_telegram_tags_nest_and_persian_offsets_count_utf16()
    {
        var (app, bot, _, user) = await StartedAsync();
        await using var _1 = app;
        const string html = """
            <b>سلام <i>دنیا</i></b> 😀 <tg-spoiler>راز</tg-spoiler> <pre><code class="language-python">x = 1</code></pre>
            <blockquote expandable>نقل</blockquote> <tg-emoji emoji-id="5">👍</tg-emoji> <a href="tg://user?id=1">Ali</a> &#1587;
            """;

        var answer = await bot.PostJsonAsync("sendMessage", new { chat_id = user, text = html, parse_mode = "HTML" });

        answer.IsOk();
        var text = answer.Result.GetProperty("text").GetString()!;
        Assert.StartsWith("سلام دنیا 😀 راز x = 1\nنقل 👍 Ali س", text, StringComparison.Ordinal);
        var types = answer.Result.GetProperty("entities").EnumerateArray().Select(e => e.GetProperty("type").GetString()).ToList();
        Assert.Equal(["bold", "italic", "spoiler", "pre", "expandable_blockquote"], types);
        var spoiler = answer.Result.GetProperty("entities")[2];
        Assert.Equal(text.IndexOf("راز", StringComparison.Ordinal), spoiler.GetProperty("offset").GetInt32());
        Assert.Equal("python", answer.Result.GetProperty("entities")[3].GetProperty("language").GetString());
    }

    [Theory]
    [Trait("Rule", "TG-25")]
    [InlineData("a<br>b")]
    [InlineData("<b>open")]
    [InlineData("<b>x</i>")]
    [InlineData("<span>x</span>")]
    public async Task TG25_unknown_or_broken_tag_is_400_and_nothing_is_stored(string html)
    {
        var (app, bot, _, user) = await StartedAsync();
        await using var _1 = app;

        (await bot.PostJsonAsync("sendMessage", new { chat_id = user, text = html, parse_mode = "HTML" }))
            .IsError(400, "Bad Request: can't parse entities");
        Assert.Single(await app.MessagesAsync());
    }

    [Fact]
    [Trait("Rule", "TG-25")]
    public async Task TG25_caption_entities_are_taken_as_given_and_unknown_kinds_left_out()
    {
        var (app, bot, _, user) = await StartedAsync();
        await using var _1 = app;
        var photo = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0, 1]);
        photo.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        using var form = new MultipartFormDataContent
        {
            { new StringContent(user.ToString(System.Globalization.CultureInfo.InvariantCulture)), "chat_id" },
            { new StringContent("cat @ali"), "caption" },
            { new StringContent("""[{"type":"italic","offset":0,"length":3},{"type":"mention","offset":4,"length":4}]"""), "caption_entities" },
            { photo, "photo", "cat.jpg" },
        };

        var answer = await bot.PostAsync("sendPhoto", form);

        answer.IsOk();
        var entity = Assert.Single(answer.Result.GetProperty("caption_entities").EnumerateArray());
        Assert.Equal("italic", entity.GetProperty("type").GetString());
        Assert.False(answer.Result.TryGetProperty("entities", out _));
    }

    [Fact]
    [Trait("Rule", "TG-14")]
    public async Task TG14_switch_buttons_are_kept_so_the_edit_works()
    {
        var (app, bot, _, user) = await StartedAsync();
        await using var _1 = app;
        var id = await SendAsync(bot, new { chat_id = user, text = "Buy milk" });
        var markup = Json("""
            {"inline_keyboard":[[{"text":"✏️","switch_inline_query_current_chat":"\nBuy milk"},{"text":"↗","switch_inline_query":"x"},
              {"text":"⤴","switch_inline_query_chosen_chat":{"query":"y","allow_user_chats":true}}]]}
            """);

        var answer = await bot.PostJsonAsync("editMessageText", new { chat_id = user, message_id = id, text = "Buy milk ✅", reply_markup = markup });

        answer.IsOk();
        var row = answer.Result.GetProperty("reply_markup").GetProperty("inline_keyboard")[0];
        Assert.Equal("\nBuy milk", row[0].GetProperty("switch_inline_query_current_chat").GetString());
        var stored = (await app.MessagesAsync())[^1].Content.InlineKeyboard!.ToRows()[0];
        Assert.Equal(["\nBuy milk", "x", "y"], stored.Select(b => b.SwitchQuery));
    }

    [Fact]
    [Trait("Rule", "TG-16")]
    public async Task TG16_edit_caption_and_wrong_kinds()
    {
        var (app, bot, _, user) = await StartedAsync();
        await using var _1 = app;
        var photo = await PhotoAsync(bot, user, "cat");
        var text = await SendAsync(bot, new { chat_id = user, text = "hello" });

        var edited = await bot.PostJsonAsync("editMessageCaption", new { chat_id = user, message_id = photo, caption = "<i>dog</i>", parse_mode = "HTML" });
        var noCaption = await bot.PostJsonAsync("editMessageCaption", new { chat_id = user, message_id = text, caption = "x" });
        var noText = await bot.PostJsonAsync("editMessageText", new { chat_id = user, message_id = photo, text = "x" });

        edited.IsOk();
        Assert.True(edited.Result.TryGetProperty("caption", out _), edited.Result.ToString());
        Assert.Equal("dog", edited.Result.GetProperty("caption").GetString());
        Assert.Equal("italic", edited.Result.GetProperty("caption_entities")[0].GetProperty("type").GetString());
        Assert.True(edited.Result.TryGetProperty("photo", out _));
        noCaption.IsError(400, "Bad Request: there is no caption in the message to edit");
        noText.IsError(400, "Bad Request: there is no text in the message to edit");

        // The photo stays through every edit, in the answer and in the database.
        var buttons = Json("""{"inline_keyboard":[[{"text":"Yes","callback_data":"y"}]]}""");
        var marked = await bot.PostJsonAsync("editMessageReplyMarkup", new { chat_id = user, message_id = photo, reply_markup = buttons });
        marked.IsOk();
        Assert.True(marked.Result.TryGetProperty("photo", out _), marked.Result.ToString());
        var stored = (await app.MessagesAsync()).Single(m => m.Seq == photo).Content;
        Assert.NotNull(stored.File);
        Assert.Equal("dog", stored.Text);
    }
}
