using System.Net;
using System.Text;
using Dalan.Relay.Tests;
using Dalan.Testing;

namespace Dalan.Telegram.Tests;

/// <summary>The address, the token, the shape of answers and the methods that need no parameters.</summary>
public class AdapterTests(PostgresServer postgres)
{
    private static readonly (string, string) LongWindow = ("Relay:RateLimitWindowSeconds", "3600");

    [Fact]
    [Trait("Rule", "TG-01")]
    [Trait("Rule", "TG-04")]
    public async Task TG04_getMe_returns_the_bot_as_a_telegram_user()
    {
        var (app, bot, _, _) = await TelegramBot.StartAsync(postgres);
        await using var _1 = app;

        (await bot.GetAsync("getMe")).ResultEquals("getMe.json");
    }

    [Theory]
    [Trait("Rule", "TG-01")]
    [InlineData("GETME")]
    [InlineData("getme")]
    public async Task TG01_method_name_matches_in_any_case(string method)
    {
        var (app, bot, _, _) = await TelegramBot.StartAsync(postgres);
        await using var _1 = app;

        (await bot.GetAsync(method)).ResultEquals("getMe.json");
    }

    [Fact]
    [Trait("Rule", "TG-01")]
    public async Task TG01_post_works_like_get()
    {
        var (app, bot, _, _) = await TelegramBot.StartAsync(postgres);
        await using var _1 = app;
        using var noBody = new ByteArrayContent([]);

        (await bot.PostAsync("getMe", noBody)).ResultEquals("getMe.json");
    }

    [Theory]
    [Trait("Rule", "TG-01")]
    [InlineData("{0}:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("999999:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("no-token-at-all")]
    public async Task TG01_wrong_token_is_401_unauthorized(string tokenPattern)
    {
        var (app, _, botId, _) = await TelegramBot.StartAsync(postgres);
        await using var _1 = app;
        var stranger = new TelegramBot(app.CreateClient(), string.Format(System.Globalization.CultureInfo.InvariantCulture, tokenPattern, botId));

        var answer = await stranger.GetAsync("getMe");

        answer.IsError(401, "Unauthorized");
        Assert.Equal("Unauthorized", answer.Description);
    }

    [Fact]
    [Trait("Rule", "TG-02")]
    public async Task TG02_every_error_has_telegrams_shape()
    {
        var (app, bot, _, _) = await TelegramBot.StartAsync(postgres, LongWindow, ("Relay:BotRequestLimit", "3"));
        await using var _1 = app;
        var stranger = new TelegramBot(app.CreateClient(), "1:wrong");

        (await stranger.GetAsync("getMe")).IsError(401, "Unauthorized");
        (await bot.GetAsync("sendMessage", "chat_id=404&text=hello")).IsError(403, "Forbidden");
        (await bot.GetAsync("sendMessage", "text=hello")).IsError(400, "Bad Request");
        (await bot.GetAsync("noSuchMethod")).IsError(400, "Bad Request");
        (await bot.GetAsync("getMe")).IsError(429, "Too Many Requests");
    }

    [Fact]
    [Trait("Rule", "TG-02")]
    public async Task TG02_persian_text_is_not_escaped()
    {
        var (app, bot, botId, _) = await TelegramBot.StartAsync(postgres);
        await using var _1 = app;
        await app.UserSaysAsync(botId, await app.AddUserAsync("سارا"), "سلام");

        var body = await (await bot.PollAsync("timeout=0")).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("سلام", body, StringComparison.Ordinal);
        Assert.Contains("سارا", body, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Rule", "TG-09")]
    [InlineData("sendSticker")]
    [InlineData("setWebhook")]
    [InlineData("noSuchMethod")]
    public async Task TG09_method_dalan_does_not_have_is_a_clear_400(string method)
    {
        var (app, bot, _, _) = await TelegramBot.StartAsync(postgres);
        await using var _1 = app;

        var answer = await bot.GetAsync(method);

        answer.IsError(400, "Bad Request");
        Assert.Equal($"Bad Request: method {method} is not supported by Dalan", answer.Description);
    }

    [Theory]
    [Trait("Rule", "TG-09")]
    [InlineData("deleteWebhook", "drop_pending_updates=true")]
    [InlineData("sendChatAction", "chat_id=1&action=typing")]
    public async Task TG09_calls_with_nothing_to_do_answer_true(string method, string query)
    {
        var (app, bot, _, _) = await TelegramBot.StartAsync(postgres);
        await using var _1 = app;

        var answer = await bot.GetAsync(method, query);

        answer.IsOk();
        Assert.True(answer.Result.GetBoolean());
    }

    [Fact]
    [Trait("Rule", "TG-10")]
    public async Task TG10_token_is_never_written_to_the_log()
    {
        var (app, bot, botId, token) = await TelegramBot.StartAsync(postgres);
        await using var _1 = app;
        var user = await app.AddUserAsync();
        await app.UserSaysAsync(botId, user, "/start");
        using var broken = new StringContent("{ not json", Encoding.UTF8, "application/json");

        (await bot.GetAsync("getMe")).IsOk();
        Assert.Single(await bot.UpdatesAsync());
        (await bot.PostJsonAsync("sendMessage", new { chat_id = user, text = "hello" })).IsOk();
        (await bot.PostAsync("sendMessage", broken)).IsError(400, "Bad Request");
        (await bot.GetAsync("noSuchMethod")).IsError(400, "Bad Request");

        var secret = token.Split(':')[1];
        Assert.DoesNotContain(app.Logs.Lines, line => line.Text.Contains(secret, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Rule", "TG-11")]
    public async Task TG11_call_over_the_bot_limit_is_429_with_retry_after()
    {
        var (app, bot, _, _) = await TelegramBot.StartAsync(postgres, LongWindow, ("Relay:BotRequestLimit", "2"));
        await using var _1 = app;

        (await bot.GetAsync("getMe")).IsOk();
        (await bot.GetAsync("getMe")).IsOk();
        var third = await bot.GetAsync("getMe");

        third.IsError(429, "Too Many Requests: retry after ");
        var seconds = third.Body.GetProperty("parameters").GetProperty("retry_after").GetInt32();
        Assert.True(seconds > 0);
        Assert.Equal(TimeSpan.FromSeconds(seconds), third.Http.Headers.RetryAfter?.Delta);
        Assert.EndsWith($" {seconds}", third.Description, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "TG-11")]
    public async Task TG11_both_doors_count_against_one_limit()
    {
        var (app, bot, _, token) = await TelegramBot.StartAsync(postgres, LongWindow, ("Relay:BotRequestLimit", "2"));
        await using var _1 = app;

        Assert.Empty(await app.BotClient(token).UpdatesAsync());
        (await bot.GetAsync("getMe")).IsOk();

        Assert.Equal(HttpStatusCode.TooManyRequests, (await bot.GetAsync("getMe")).Status);
    }
}
