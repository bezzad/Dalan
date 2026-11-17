using System.Text.Json;
using Dalan.Relay.Chats;
using Dalan.Relay.Tests;
using Dalan.Testing;

namespace Dalan.Telegram.Tests;

public class GetUpdatesTests(PostgresServer postgres)
{
    private static readonly TimeSpan Soon = TimeSpan.FromSeconds(30);

    [Fact]
    [Trait("Rule", "TG-05")]
    public async Task TG05_update_has_telegrams_shape()
    {
        var (app, bot, botId, _) = await TelegramBot.StartAsync(postgres);
        await using var _1 = app;
        await app.UserSaysAsync(botId, await app.AddUserAsync("Sara", "sara"), "hello");

        (await bot.GetAsync("getUpdates", "timeout=0")).ResultEquals("update.json");
    }

    [Fact]
    [Trait("Rule", "TG-05")]
    public async Task TG05_user_without_a_username_has_no_username_field()
    {
        var (app, bot, botId, _) = await TelegramBot.StartAsync(postgres);
        await using var _1 = app;
        await app.UserSaysAsync(botId, await app.AddUserAsync("Sara"), "hello");

        var message = Assert.Single(await bot.UpdatesAsync()).GetProperty("message");

        Assert.False(message.GetProperty("from").TryGetProperty("username", out _));
        Assert.False(message.GetProperty("chat").TryGetProperty("username", out _));
    }

    [Fact]
    [Trait("Rule", "TG-05")]
    public async Task TG05_offset_confirms_the_update()
    {
        var (app, bot, botId, _) = await TelegramBot.StartAsync(postgres);
        await using var _1 = app;
        var message = await app.UserSaysAsync(botId, await app.AddUserAsync(), "hello");

        var update = Assert.Single(await bot.UpdatesAsync());
        Assert.Equal(message.Id, update.GetProperty("update_id").GetInt64());
        Assert.Equal(DeliveryState.Pending, await app.DeliveryOfAsync(message));

        Assert.Empty(await bot.UpdatesAsync($"timeout=0&offset={message.Id + 1}"));
        Assert.Equal(DeliveryState.Delivered, await app.DeliveryOfAsync(message));
    }

    [Theory]
    [Trait("Rule", "TG-05")]
    [InlineData("offset=-1&timeout=0")]
    [InlineData("limit=0&timeout=0")]
    [InlineData("limit=-5&timeout=-3")]
    public async Task TG05_values_under_the_range_are_cut_not_refused(string query)
    {
        var (app, bot, botId, _) = await TelegramBot.StartAsync(postgres);
        await using var _1 = app;
        await app.UserSaysAsync(botId, await app.AddUserAsync(), "hello");

        Assert.Single(await bot.UpdatesAsync(query));
    }

    [Fact]
    [Trait("Rule", "TG-05")]
    public async Task TG05_limit_cuts_the_batch()
    {
        var (app, bot, botId, _) = await TelegramBot.StartAsync(postgres);
        await using var _1 = app;
        foreach (var name in new[] { "a", "b", "c" })
            await app.UserSaysAsync(botId, await app.AddUserAsync(name), name);

        var firstTwo = await bot.UpdatesAsync("timeout=0&limit=2");

        Assert.Equal(["a", "b"], firstTwo.Select(u => u.GetProperty("message").GetProperty("text").GetString()));
    }

    [Fact]
    [Trait("Rule", "TG-05")]
    public async Task TG05_parameters_may_come_as_json()
    {
        var (app, bot, botId, _) = await TelegramBot.StartAsync(postgres);
        await using var _1 = app;
        var message = await app.UserSaysAsync(botId, await app.AddUserAsync(), "hello");
        Assert.Single(await bot.UpdatesAsync());

        var answer = await bot.PostJsonAsync("getUpdates", JsonSerializer.Deserialize<JsonElement>(
            $$"""{"offset":{{message.Id + 1}},"timeout":0,"allowed_updates":["message"]}"""));

        answer.IsOk();
        Assert.Equal(0, answer.Result.GetArrayLength());
        Assert.Equal(DeliveryState.Delivered, await app.DeliveryOfAsync(message));
    }

    [Fact]
    [Trait("Rule", "TG-05")]
    public async Task TG05_waiting_poll_returns_when_a_message_arrives()
    {
        var (app, bot, botId, _) = await TelegramBot.StartAsync(postgres);
        await using var _1 = app;
        var user = await app.AddUserAsync();

        var poll = bot.PollAsync("timeout=50");
        await app.Queue().PollWaitsAsync();
        Assert.False(poll.IsCompleted);
        await app.UserSaysAsync(botId, user, "hello");

        var answer = await TelegramBot.ReadAsync(await poll.WaitAsync(Soon, TestContext.Current.CancellationToken));
        answer.IsOk();
        Assert.Equal("hello", Assert.Single(answer.Result.EnumerateArray().ToArray()).GetProperty("message").GetProperty("text").GetString());
    }

    [Fact]
    [Trait("Rule", "TG-05")]
    public async Task TG05_second_poll_replaces_the_first_which_gets_409_conflict()
    {
        var (app, bot, _, _) = await TelegramBot.StartAsync(postgres);
        await using var _1 = app;

        var first = bot.PollAsync("timeout=50");
        await app.Queue().PollWaitsAsync();
        var second = bot.PollAsync("timeout=50");
        await app.Queue().PollWaitsAsync();

        var replaced = await TelegramBot.ReadAsync(await first.WaitAsync(Soon, TestContext.Current.CancellationToken));
        replaced.IsError(409, "Conflict");
        Assert.False(second.IsCompleted);
    }

    [Fact]
    [Trait("Rule", "TG-05")]
    public async Task TG05_update_does_not_contain_the_phone_number()
    {
        var (app, bot, botId, _) = await TelegramBot.StartAsync(postgres);
        await using var _1 = app;
        await app.UserSaysAsync(botId, await app.AddUserAsync(phone: "+989121234567"), "hello");

        var body = await (await bot.PollAsync("timeout=0")).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("hello", body, StringComparison.Ordinal);
        Assert.DoesNotContain("9121234567", body, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Rule", "TG-06")]
    [InlineData("/start", 0, 6)]
    [InlineData("/help@testbot now", 0, 13)]
    [InlineData("سلام /menu", 5, 5)]
    [InlineData("line\n/a_1", 5, 4)]
    public async Task TG06_command_is_marked_with_an_entity(string text, int offset, int length)
    {
        var entity = Assert.Single((await EntitiesAsync(text))!.Value.EnumerateArray().ToArray());

        Assert.Equal("bot_command", entity.GetProperty("type").GetString());
        Assert.Equal((offset, length), (entity.GetProperty("offset").GetInt32(), entity.GetProperty("length").GetInt32()));
    }

    [Fact]
    [Trait("Rule", "TG-06")]
    public async Task TG06_every_command_of_a_text_is_marked() =>
        Assert.Equal(2, (await EntitiesAsync("/one then /two"))!.Value.GetArrayLength());

    [Theory]
    [Trait("Rule", "TG-06")]
    [InlineData("hello")]
    [InlineData("1/2")]
    [InlineData("a/b")]
    [InlineData("/")]
    [InlineData("/a_name_that_is_longer_than_32_letters")]
    public async Task TG06_text_without_a_command_has_no_entities(string text) =>
        Assert.Null(await EntitiesAsync(text));

    private async Task<JsonElement?> EntitiesAsync(string text)
    {
        var (app, bot, botId, _) = await TelegramBot.StartAsync(postgres);
        await using var _1 = app;
        await app.UserSaysAsync(botId, await app.AddUserAsync(), text);

        var message = Assert.Single(await bot.UpdatesAsync()).GetProperty("message");
        return message.TryGetProperty("entities", out var entities) ? entities : null;
    }
}
