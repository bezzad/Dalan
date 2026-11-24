using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dalan.Relay.Chats;
using Dalan.Relay.Tests;
using Dalan.Testing;

namespace Dalan.Telegram.Tests;

public class SendMessageTests(PostgresServer postgres)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    /// <summary>A bot and a user who started it.</summary>
    private async Task<(DalanApp App, TelegramBot Bot, long User)> StartedAsync(params (string, string)[] settings)
    {
        var (app, bot, botId, _) = await TelegramBot.StartAsync(postgres, settings);
        var user = await app.AddUserAsync();
        await app.UserSaysAsync(botId, user, "/start");
        return (app, bot, user);
    }

    private static async Task<int> BotMessagesAsync(DalanApp app) =>
        (await app.MessagesAsync()).Count(m => m.Direction == Direction.FromBot);

    [Fact]
    [Trait("Rule", "TG-07")]
    public async Task TG07_answer_is_a_telegram_message_and_is_stored()
    {
        var (app, bot, user) = await StartedAsync();
        await using var _1 = app;

        (await bot.PostJsonAsync("sendMessage", new { chat_id = user, text = "hello" })).ResultEquals("sentMessage.json");

        var stored = (await app.MessagesAsync())[^1];
        Assert.Equal((Direction.FromBot, user, "hello"), (stored.Direction, stored.UserId, stored.Content.Text));
    }

    [Fact]
    [Trait("Rule", "TG-07")]
    public async Task TG07_user_who_never_started_the_bot_is_403()
    {
        var (app, bot, _, _) = await TelegramBot.StartAsync(postgres);
        await using var _1 = app;
        var stranger = await app.AddUserAsync();

        (await bot.PostJsonAsync("sendMessage", new { chat_id = stranger, text = "spam" })).IsError(403, "Forbidden");
        Assert.Empty(await app.MessagesAsync());
    }

    [Fact]
    [Trait("Rule", "TG-07")]
    public async Task TG07_user_who_stopped_the_bot_is_403()
    {
        var (app, bot, user) = await StartedAsync();
        await using var _1 = app;
        await PostgresServer.ExecuteAsync(app.ConnectionString, $"UPDATE relay.memberships SET state = {(int)MembershipState.Stopped}");

        (await bot.PostJsonAsync("sendMessage", new { chat_id = user, text = "come back" })).IsError(403, "Forbidden");
    }

    [Theory]
    [Trait("Rule", "TG-07")]
    [InlineData(null, "Bad Request: message text is empty")]
    [InlineData("", "Bad Request: message text is empty")]
    [InlineData("12345678901", "Bad Request: message is too long")]
    public async Task TG07_text_must_be_there_and_fit_the_limit(string? text, string description)
    {
        var (app, bot, user) = await StartedAsync(("Relay:MaxTextLength", "10"));
        await using var _1 = app;

        var answer = await bot.PostJsonAsync("sendMessage", new { chat_id = user, text });

        answer.IsError(400, "Bad Request");
        Assert.Equal(description, answer.Description);
        Assert.Equal(0, await BotMessagesAsync(app));
    }

    [Theory]
    [Trait("Rule", "TG-07")]
    [InlineData("text=hello")]
    [InlineData("chat_id=%40channel&text=hello")]
    public async Task TG07_chat_that_cannot_be_is_chat_not_found(string query)
    {
        var (app, bot, _) = await StartedAsync();
        await using var _1 = app;

        var answer = await bot.GetAsync("sendMessage", query);

        answer.IsError(400, "Bad Request");
        Assert.Equal("Bad Request: chat not found", answer.Description);
    }

    [Fact]
    [Trait("Rule", "TG-07")]
    public async Task TG07_unknown_parameters_are_ignored_and_the_text_is_kept()
    {
        var (app, bot, user) = await StartedAsync();
        await using var _1 = app;

        (await bot.PostJsonAsync("sendMessage",
            new { chat_id = user, text = "*hi*", parse_mode = "MarkdownV2", disable_notification = true })).IsOk();

        Assert.Equal("*hi*", (await app.MessagesAsync())[^1].Content.Text);
    }

    [Fact]
    [Trait("Rule", "TG-03")]
    public async Task TG03_parameters_in_query_form_multipart_or_json_all_work()
    {
        var (app, bot, user) = await StartedAsync();
        await using var _1 = app;
        var id = user.ToString(System.Globalization.CultureInfo.InvariantCulture);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["chat_id"] = id, ["text"] = "form" });
        using var multipart = new MultipartFormDataContent { { new StringContent(id), "chat_id" }, { new StringContent("multipart"), "text" } };

        (await bot.GetAsync("sendMessage", $"chat_id={id}&text=query")).IsOk();
        (await bot.PostAsync("sendMessage", form)).IsOk();
        (await bot.PostAsync("sendMessage", multipart)).IsOk();
        (await bot.PostJsonAsync("sendMessage", new { chat_id = user, text = "json" })).IsOk();

        Assert.Equal(["query", "form", "multipart", "json"],
            (await app.MessagesAsync()).Where(m => m.Direction == Direction.FromBot).Select(m => m.Content.Text));
    }

    [Fact]
    [Trait("Rule", "TG-03")]
    public async Task TG03_number_may_come_as_a_string()
    {
        var (app, bot, user) = await StartedAsync();
        await using var _1 = app;

        (await bot.PostJsonAsync("sendMessage", new { chat_id = user.ToString(System.Globalization.CultureInfo.InvariantCulture), text = "hi" })).IsOk();

        Assert.Equal(user, (await app.MessagesAsync())[^1].UserId);
    }

    [Theory]
    [Trait("Rule", "TG-03")]
    [InlineData("{ not json")]
    [InlineData("[1,2]")]
    public async Task TG03_body_that_cannot_be_read_is_400(string body)
    {
        var (app, bot, _) = await StartedAsync();
        await using var _1 = app;
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        (await bot.PostAsync("sendMessage", content)).IsError(400, "Bad Request");
        Assert.Equal(0, await BotMessagesAsync(app));
    }

    [Fact]
    [Trait("Rule", "TG-08")]
    public async Task TG08_reply_keyboard_object_becomes_the_keyboard()
    {
        var (app, bot, user) = await StartedAsync();
        await using var _1 = app;
        var markup = JsonSerializer.Deserialize<JsonElement>(
            """{"keyboard":[[{"text":"Yes"},"No"],["Cancel"]],"resize_keyboard":true,"one_time_keyboard":true}""");

        (await bot.PostJsonAsync("sendMessage", new { chat_id = user, text = "Sure?", reply_markup = markup })).IsOk();

        Assert.Equal([["Yes", "No"], ["Cancel"]], (await app.MessagesAsync())[^1].Content.ReplyKeyboard!.ToRows());
    }

    [Fact]
    [Trait("Rule", "TG-08")]
    public async Task TG08_reply_keyboard_as_a_string_in_a_form_works_too()
    {
        var (app, bot, user) = await StartedAsync();
        await using var _1 = app;
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["chat_id"] = user.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["text"] = "Sure?",
            ["reply_markup"] = """{"keyboard":[[{"text":"Yes"},"No"],["Cancel"]]}""",
        });

        (await bot.PostAsync("sendMessage", form)).IsOk();

        Assert.Equal([["Yes", "No"], ["Cancel"]], (await app.MessagesAsync())[^1].Content.ReplyKeyboard!.ToRows());
    }

    [Fact]
    [Trait("Rule", "TG-08")]
    public async Task TG08_remove_keyboard_gives_an_empty_keyboard_in_the_history()
    {
        var (app, bot, user) = await StartedAsync();
        await using var _1 = app;

        (await bot.PostJsonAsync("sendMessage", new { chat_id = user, text = "bye", reply_markup = new { remove_keyboard = true } })).IsOk();

        var stored = (await app.MessagesAsync())[^1].Content.ReplyKeyboard;
        Assert.NotNull(stored);
        Assert.Empty(stored.Rows);
    }

    [Theory]
    [Trait("Rule", "TG-08")]
    [InlineData("""{"inline_keyboard":[[{"text":"A","pay":true}]]}""")]
    [InlineData("""{"force_reply":true}""")]
    [InlineData("""{"keyboard":[[{"text":"Phone","request_contact":true}]]}""")]
    [InlineData("""{"keyboard":[[{"text":"App","web_app":{"url":"https://example.ir"}}]]}""")]
    [InlineData("""{"keyboard":[[{"no_text":1}]]}""")]
    [InlineData("""{"keyboard":"Yes"}""")]
    [InlineData("\"not json at all")]
    public async Task TG08_markup_dalan_cannot_serve_is_400_and_nothing_is_stored(string markup)
    {
        var (app, bot, user) = await StartedAsync();
        await using var _1 = app;
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["chat_id"] = user.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["text"] = "hi",
            ["reply_markup"] = markup,
        });

        (await bot.PostAsync("sendMessage", form)).IsError(400, "Bad Request");
        Assert.Equal(0, await BotMessagesAsync(app));
    }

    [Fact]
    [Trait("Rule", "TG-08")]
    public async Task TG08_keyboard_over_the_limits_is_400()
    {
        var (app, bot, user) = await StartedAsync(("Relay:MaxKeyboardButtons", "2"));
        await using var _1 = app;

        var markup = JsonSerializer.Deserialize<JsonElement>("""{"keyboard":[["a","b","c"]]}""");

        (await bot.PostJsonAsync("sendMessage", new { chat_id = user, text = "hi", reply_markup = markup })).IsError(400, "Bad Request");
        Assert.Equal(0, await BotMessagesAsync(app));
    }

    [Fact]
    [Trait("Rule", "REL-19")]
    public async Task REL19_update_read_on_the_core_door_is_confirmed_on_the_telegram_door()
    {
        var (app, bot, botId, token) = await TelegramBot.StartAsync(postgres);
        await using var _1 = app;
        var message = await app.UserSaysAsync(botId, await app.AddUserAsync(), "hello");

        Assert.Single(await app.BotClient(token).UpdatesAsync());
        Assert.Empty(await bot.UpdatesAsync($"timeout=0&offset={message.Id + 1}"));

        Assert.Equal(DeliveryState.Delivered, await app.DeliveryOfAsync(message));
        Assert.Empty(await app.BotClient(token).UpdatesAsync());
    }

    [Fact]
    [Trait("Rule", "REL-19")]
    public async Task REL19_both_doors_write_to_the_same_chat()
    {
        var (app, bot, botId, token) = await TelegramBot.StartAsync(postgres);
        await using var _1 = app;
        var user = await app.AddUserAsync();
        await app.UserSaysAsync(botId, user, "/start");

        (await app.BotClient(token).SendAsync(user, "core")).EnsureSuccessStatusCode();
        (await bot.PostJsonAsync("sendMessage", new { chat_id = user, text = "telegram" })).IsOk();

        var bots = (await app.MessagesAsync()).Where(m => m.Direction == Direction.FromBot).ToList();
        Assert.Equal([("core", 2L), ("telegram", 3L)], bots.Select(m => (m.Content.Text, m.Seq)));
    }
}
