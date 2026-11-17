using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dalan.Relay.Chats;
using Dalan.Testing;

namespace Dalan.Relay.Tests;

public class ClientApiTests(PostgresServer postgres)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Theory]
    [Trait("Rule", "REL-12")]
    [InlineData("/api/bots")]
    [InlineData("/api/bots/by-username/anybot")]
    [InlineData("/api/bots/1/messages")]
    [InlineData("/api/events")]
    [InlineData("/api/settings")]
    public async Task REL12_call_without_a_session_is_401(string path)
    {
        await using var app = await RelayApp.StartAsync(postgres);

        var response = await app.Browser().GetAsync(path, Cancel);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthorized", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "REL-12")]
    public async Task REL12_post_without_a_session_is_401_and_stores_nothing()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, _) = await app.AddBotAsync();

        var response = await app.Browser().SayAsync(botId, "/start");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await app.MessagesAsync());
    }

    [Fact]
    [Trait("Rule", "REL-12")]
    public async Task REL12_call_with_a_session_acts_as_that_user()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, _) = await app.AddBotAsync();
        var (browser, userId) = await app.LoginAsync();

        await browser.SayAsync(botId, "/start");

        Assert.Equal(userId, Assert.Single(await app.MessagesAsync()).UserId);
    }

    [Fact]
    [Trait("Rule", "REL-13")]
    public async Task REL13_bot_is_found_by_username_in_any_letter_case()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, _) = await app.AddBotAsync("ArcaneBot");
        var (browser, _) = await app.LoginAsync();

        var bot = await browser.JsonAsync("/api/bots/by-username/arcanebot");

        Assert.Equal(botId, bot.GetProperty("id").GetInt64());
        Assert.Equal("ArcaneBot", bot.GetProperty("username").GetString());
        Assert.Equal("Test bot", bot.GetProperty("name").GetString());
        Assert.False(bot.GetProperty("started").GetBoolean());
    }

    [Fact]
    [Trait("Rule", "REL-13")]
    public async Task REL13_started_is_true_after_the_first_message()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, _) = await app.AddBotAsync("ArcaneBot");
        var (browser, _) = await app.LoginAsync();
        var (other, _) = await app.LoginAsync("Other");
        await browser.SayAsync(botId, "/start");

        Assert.True((await browser.JsonAsync("/api/bots/by-username/ArcaneBot")).GetProperty("started").GetBoolean());
        Assert.False((await other.JsonAsync("/api/bots/by-username/ArcaneBot")).GetProperty("started").GetBoolean());
    }

    [Fact]
    [Trait("Rule", "REL-13")]
    public async Task REL13_unknown_username_is_404()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (browser, _) = await app.LoginAsync();

        var response = await browser.GetAsync("/api/bots/by-username/nobody", Cancel);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("bot_not_found", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "REL-14")]
    public async Task REL14_list_holds_only_my_bots_newest_chat_first()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (first, _) = await app.AddBotAsync("first");
        var (second, _) = await app.AddBotAsync("second");
        var (theirs, _) = await app.AddBotAsync("theirs");
        var (me, _) = await app.LoginAsync("Me");
        var (them, _) = await app.LoginAsync("Them");
        await me.SayAsync(first, "/start");
        app.Time.Advance(TimeSpan.FromSeconds(1));
        await me.SayAsync(second, "/start");
        await them.SayAsync(theirs, "/start");

        var bots = (await me.JsonAsync("/api/bots")).GetProperty("bots").EnumerateArray().ToList();

        Assert.Equal(["second", "first"], bots.Select(b => b.GetProperty("username").GetString()));
        Assert.Equal(second, bots[0].GetProperty("id").GetInt64());
        Assert.Equal("Test bot", bots[0].GetProperty("name").GetString());
    }

    [Fact]
    [Trait("Rule", "REL-14")]
    public async Task REL14_new_user_has_an_empty_list()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        await app.AddBotAsync();
        var (browser, _) = await app.LoginAsync();

        Assert.Equal(0, (await browser.JsonAsync("/api/bots")).GetProperty("bots").GetArrayLength());
    }

    [Fact]
    [Trait("Rule", "REL-15")]
    [Trait("Rule", "Q-01")]
    public async Task REL15_start_is_saved_returned_and_reaches_the_bot()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, token) = await app.AddBotAsync();
        var (browser, userId) = await app.LoginAsync("Sara");

        var response = await browser.SayAsync(botId, "/start");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(Cancel);
        Assert.Equal(1, json.GetProperty("id").GetInt64());
        Assert.Equal("user", json.GetProperty("from").GetString());
        Assert.Equal("/start", json.GetProperty("text").GetString());
        Assert.Equal(app.Time.GetUtcNow(), json.GetProperty("date").GetDateTimeOffset());

        var update = Assert.Single(await app.BotClient(token).UpdatesAsync());
        Assert.Equal("/start", update.Text());
        Assert.Equal(userId, update.GetProperty("message").GetProperty("from").GetProperty("id").GetInt64());
        Assert.Equal("Sara", update.GetProperty("message").GetProperty("from").GetProperty("name").GetString());
    }

    [Fact]
    [Trait("Rule", "REL-15")]
    public async Task REL15_unknown_bot_is_404_and_stores_nothing()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (browser, _) = await app.LoginAsync();

        var response = await browser.SayAsync(botId: 404, "/start");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("bot_not_found", await response.ErrorCodeAsync());
        Assert.Empty(await app.MessagesAsync());
    }

    [Theory]
    [Trait("Rule", "REL-15")]
    [Trait("Rule", "REL-09")]
    [InlineData(0, "text_empty")]
    [InlineData(11, "text_too_long")]
    public async Task REL15_user_text_must_fit_the_limit(int length, string expectedError)
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Relay:MaxTextLength", "10"));
        var (botId, _) = await app.AddBotAsync();
        var (browser, _) = await app.LoginAsync();

        var response = await browser.SayAsync(botId, new string('x', length));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expectedError, await response.ErrorCodeAsync());
        Assert.Empty(await app.MessagesAsync());
    }

    [Theory]
    [Trait("Rule", "REL-15")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"text":5}""")]
    public async Task REL15_body_that_is_not_valid_is_400(string body)
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, _) = await app.AddBotAsync();
        var (browser, _) = await app.LoginAsync();
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await browser.PostAsync($"/api/bots/{botId}/messages", content, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_request", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "SEC-05")]
    public async Task SEC05_form_post_from_another_site_changes_nothing()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, _) = await app.AddBotAsync();
        var (browser, _) = await app.LoginAsync();
        using var form = new FormUrlEncodedContent([new("text", "/start")]);

        var response = await browser.PostAsync($"/api/bots/{botId}/messages", form, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_request", await response.ErrorCodeAsync());
        Assert.Empty(await app.MessagesAsync());
    }

    /// <summary>A chat with the messages 1 to 5: the user and the bot take turns.</summary>
    private static async Task<(HttpClient Browser, long BotId)> ChatWithFiveMessagesAsync(DalanApp app)
    {
        var (botId, token) = await app.AddBotAsync();
        var (browser, userId) = await app.LoginAsync();
        var bot = app.BotClient(token);
        for (var i = 1; i <= 5; i++)
        {
            var response = i % 2 == 1 ? await browser.SayAsync(botId, $"text {i}") : await bot.SendAsync(userId, $"text {i}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        return (browser, botId);
    }

    [Fact]
    [Trait("Rule", "REL-16")]
    public async Task REL16_open_returns_the_newest_messages_in_rising_order()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (browser, botId) = await ChatWithFiveMessagesAsync(app);

        var messages = await browser.HistoryAsync(botId, "limit=3");

        Assert.Equal([3, 4, 5], messages.Ids());
        Assert.Equal(["user", "bot", "user"], messages.Select(m => m.GetProperty("from").GetString()));
        Assert.Equal("text 4", messages[1].GetProperty("text").GetString());
        Assert.Equal(app.Time.GetUtcNow(), messages[1].GetProperty("date").GetDateTimeOffset());
    }

    [Theory]
    [Trait("Rule", "REL-16")]
    [InlineData("")]
    [InlineData("limit=50")]
    public async Task REL16_page_is_never_above_the_maximum(string query)
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Relay:MaxMessagesPerSync", "2"));
        var (browser, botId) = await ChatWithFiveMessagesAsync(app);

        var messages = await browser.HistoryAsync(botId, query);

        Assert.Equal([4, 5], messages.Ids());
    }

    [Fact]
    [Trait("Rule", "REL-16")]
    public async Task REL16_after_returns_the_oldest_messages_after_that_number()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (browser, botId) = await ChatWithFiveMessagesAsync(app);

        var afterTwo = await browser.HistoryAsync(botId, "after=2&limit=2");
        var afterNewest = await browser.HistoryAsync(botId, "after=5");

        Assert.Equal([3, 4], afterTwo.Ids());
        Assert.Empty(afterNewest);
    }

    [Fact]
    [Trait("Rule", "REL-16")]
    public async Task REL16_before_returns_the_newest_messages_before_that_number()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (browser, botId) = await ChatWithFiveMessagesAsync(app);

        var messages = await browser.HistoryAsync(botId, "before=4&limit=2");

        Assert.Equal([2, 3], messages.Ids());
    }

    [Fact]
    [Trait("Rule", "REL-16")]
    public async Task REL16_another_users_messages_are_never_returned()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, _) = await app.AddBotAsync();
        var (alice, _) = await app.LoginAsync("Alice");
        var (bob, _) = await app.LoginAsync("Bob");
        await alice.SayAsync(botId, "from alice");
        await bob.SayAsync(botId, "from bob");
        var (stranger, _) = await app.LoginAsync("Stranger");

        var message = Assert.Single(await alice.HistoryAsync(botId));
        Assert.Equal("from alice", message.GetProperty("text").GetString());
        Assert.Empty(await stranger.HistoryAsync(botId));
    }

    [Theory]
    [Trait("Rule", "REL-16")]
    [InlineData("after=abc")]
    [InlineData("before=-1")]
    [InlineData("limit=0")]
    public async Task REL16_bad_parameter_is_400(string query)
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, _) = await app.AddBotAsync();
        var (browser, _) = await app.LoginAsync();

        var response = await browser.GetAsync($"/api/bots/{botId}/messages?{query}", Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_request", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "REL-17")]
    public async Task REL17_keyboard_is_returned_to_the_bot_and_shown_to_the_user()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, token) = await app.AddBotAsync();
        var (browser, userId) = await app.LoginAsync();
        await browser.SayAsync(botId, "/start");
        string[][] keyboard = [["بله", "No"], ["Cancel"]];

        var response = await app.BotClient(token).SendAsync(userId, "Sure?", keyboard);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sent = await response.Content.ReadFromJsonAsync<JsonElement>(Cancel);
        Assert.Equal(keyboard, sent.GetProperty("reply_keyboard").Deserialize<string[][]>());
        var shown = (await browser.HistoryAsync(botId))[^1];
        Assert.Equal(keyboard, shown.GetProperty("reply_keyboard").Deserialize<string[][]>());
    }

    [Fact]
    [Trait("Rule", "REL-17")]
    public async Task REL17_message_without_a_keyboard_has_no_keyboard_field()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, token) = await app.AddBotAsync();
        var (browser, userId) = await app.LoginAsync();
        await browser.SayAsync(botId, "/start");

        var response = await app.BotClient(token).SendAsync(userId, "plain");

        Assert.False((await response.Content.ReadFromJsonAsync<JsonElement>(Cancel)).TryGetProperty("reply_keyboard", out _));
        foreach (var message in await browser.HistoryAsync(botId))
            Assert.False(message.TryGetProperty("reply_keyboard", out _));
    }

    [Fact]
    [Trait("Rule", "REL-17")]
    public async Task REL17_empty_keyboard_is_kept_as_an_empty_list()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, token) = await app.AddBotAsync();
        var (browser, userId) = await app.LoginAsync();
        await browser.SayAsync(botId, "/start");

        await app.BotClient(token).SendAsync(userId, "Done", []);

        var shown = (await browser.HistoryAsync(botId))[^1];
        Assert.Equal(0, shown.GetProperty("reply_keyboard").GetArrayLength());
    }

    [Theory]
    [Trait("Rule", "REL-17")]
    [InlineData("""[["a","b"],["c","d"]]""")] // 4 buttons, the limit is 3
    [InlineData("""[["ok",""]]""")]
    [InlineData("""[["123456"]]""")] // 6 characters, the limit is 5
    [InlineData("""[null]""")]
    [InlineData("""[[null]]""")]
    public async Task REL17_keyboard_over_the_limits_is_400(string keyboard)
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Relay:MaxKeyboardButtons", "3"), ("Relay:MaxButtonTextLength", "5"));
        var (botId, token) = await app.AddBotAsync();
        var (browser, userId) = await app.LoginAsync();
        await browser.SayAsync(botId, "/start");
        using var content = new StringContent(
            $$"""{"user_id":{{userId}},"text":"hi","reply_keyboard":{{keyboard}}}""", Encoding.UTF8, "application/json");

        var response = await app.BotClient(token).PostAsync("/v1/messages", content, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_keyboard", await response.ErrorCodeAsync());
        Assert.Single(await app.MessagesAsync());
    }

    [Fact]
    [Trait("Rule", "REL-17")]
    public async Task REL17_keyboard_at_the_limits_is_accepted()
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Relay:MaxKeyboardButtons", "3"), ("Relay:MaxButtonTextLength", "5"));
        var (botId, token) = await app.AddBotAsync();
        var (browser, userId) = await app.LoginAsync();
        await browser.SayAsync(botId, "/start");

        var response = await app.BotClient(token).SendAsync(userId, "hi", [["12345", "b"], ["c"]]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = (await app.MessagesAsync())[^1];
        Assert.Equal(Direction.FromBot, stored.Direction);
        Assert.Equal(["12345", "b"], stored.Content.ReplyKeyboard!.Rows[0].Buttons);
    }

    [Fact]
    [Trait("Rule", "REL-18")]
    public async Task REL18_settings_come_from_the_server()
    {
        await using var app = await RelayApp.StartAsync(postgres,
            ("Relay:MaxTextLength", "10"), ("Relay:MaxMessagesPerSync", "7"),
            ("Relay:ClientReconnectMinSeconds", "2"), ("Relay:ClientReconnectMaxSeconds", "40"));
        var (browser, _) = await app.LoginAsync();

        var settings = await browser.JsonAsync("/api/settings");

        Assert.Equal(10, settings.GetProperty("max_text_length").GetInt32());
        Assert.Equal(7, settings.GetProperty("sync_page_size").GetInt32());
        Assert.Equal(2, settings.GetProperty("reconnect_min_seconds").GetInt32());
        Assert.Equal(40, settings.GetProperty("reconnect_max_seconds").GetInt32());
    }

    [Fact]
    [Trait("Rule", "REL-11")]
    public async Task REL11_every_client_api_error_has_a_code_and_a_message()
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Relay:UserRequestLimit", "4"), ("Relay:RateLimitWindowSeconds", "3600"));
        var (browser, _) = await app.LoginAsync();
        HttpResponseMessage[] errors =
        [
            await app.Browser().GetAsync("/api/bots", Cancel),                 // 401
            await browser.GetAsync("/api/bots/1/messages?after=x", Cancel),    // 400
            await browser.GetAsync("/api/bots/by-username/nobody", Cancel),    // 404
            await browser.SayAsync(botId: 404, "hello"),                       // 404
            await browser.SayAsync(botId: 404, ""),                            // 400
            await browser.GetAsync("/api/bots", Cancel),                       // 429
        ];

        Assert.Equal([401, 400, 404, 404, 400, 429], errors.Select(e => (int)e.StatusCode));
        foreach (var error in errors)
            Assert.False(string.IsNullOrWhiteSpace(await error.ErrorCodeAsync()));
    }
}
