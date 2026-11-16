using System.Net;
using Dalan.Relay.Chats;
using Dalan.Relay.Queue;
using Dalan.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dalan.Relay.Tests;

public class GetUpdatesTests(PostgresServer postgres)
{
    private static readonly TimeSpan Soon = TimeSpan.FromSeconds(30);

    [Fact]
    [Trait("Rule", "REL-07")]
    public async Task REL07_update_carries_the_message_and_its_sender()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, token) = await app.AddBotAsync();
        var user = await app.AddUserAsync("Sara", "sara");
        var message = await app.UserSaysAsync(botId, user, "/start");

        var update = Assert.Single(await app.BotClient(token).UpdatesAsync());

        Assert.Equal(message.Id, update.Id());
        var json = update.GetProperty("message");
        Assert.Equal(1, json.GetProperty("id").GetInt64());
        Assert.Equal("/start", json.GetProperty("text").GetString());
        Assert.Equal(app.Time.GetUtcNow(), json.GetProperty("date").GetDateTimeOffset());
        Assert.Equal(user, json.GetProperty("from").GetProperty("id").GetInt64());
        Assert.Equal("Sara", json.GetProperty("from").GetProperty("name").GetString());
        Assert.Equal("sara", json.GetProperty("from").GetProperty("username").GetString());
    }

    [Fact]
    [Trait("Rule", "REL-07")]
    public async Task REL07_bad_parameter_is_400()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (_, token) = await app.AddBotAsync();

        var response = await app.BotClient(token).GetAsync("/v1/updates?limit=abc", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_request", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "REL-07")]
    public async Task REL07_persian_text_is_not_escaped()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, token) = await app.AddBotAsync();
        await app.UserSaysAsync(botId, await app.AddUserAsync("سارا"), "سلام");

        var body = await app.BotClient(token).GetStringAsync("/v1/updates?timeout=0", TestContext.Current.CancellationToken);

        Assert.Contains("سلام", body, StringComparison.Ordinal);
        Assert.Contains("سارا", body, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "REL-10")]
    public async Task REL10_update_does_not_contain_the_phone_number()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, token) = await app.AddBotAsync();
        var user = await app.AddUserAsync(phone: "+989121234567");
        await app.UserSaysAsync(botId, user, "hello");

        var body = await app.BotClient(token).GetStringAsync("/v1/updates?timeout=0", TestContext.Current.CancellationToken);

        Assert.Contains("hello", body, StringComparison.Ordinal);
        Assert.DoesNotContain("9121234567", body, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "Q-03")]
    public async Task Q03_message_is_delivered_only_after_the_offset_confirms_it()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, token) = await app.AddBotAsync();
        var bot = app.BotClient(token);
        var message = await app.UserSaysAsync(botId, await app.AddUserAsync(), "hello");

        var first = Assert.Single(await bot.UpdatesAsync());
        Assert.Equal(DeliveryState.Pending, await app.DeliveryOfAsync(message));

        var again = Assert.Single(await bot.UpdatesAsync($"timeout=0&offset={first.Id()}"));
        Assert.Equal(first.Id(), again.Id());
        Assert.Equal(DeliveryState.Pending, await app.DeliveryOfAsync(message));

        Assert.Empty(await bot.UpdatesAsync($"timeout=0&offset={first.Id() + 1}"));
        Assert.Equal(DeliveryState.Delivered, await app.DeliveryOfAsync(message));
    }

    [Fact]
    [Trait("Rule", "Q-02")]
    public async Task Q02_expired_update_is_not_returned_and_is_marked_not_delivered()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, token) = await app.AddBotAsync();
        var message = await app.UserSaysAsync(botId, await app.AddUserAsync(), "hello");

        app.Time.Advance(TimeSpan.FromSeconds(60));

        Assert.Empty(await app.BotClient(token).UpdatesAsync());
        Assert.Equal(DeliveryState.NotDelivered, await app.DeliveryOfAsync(message));
    }

    [Fact]
    [Trait("Rule", "Q-02")]
    public async Task Q02_sweep_marks_expired_messages_while_the_bot_is_away()
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Relay:LatestCommandWins", "false"));
        var (botId, _) = await app.AddBotAsync();
        var user = await app.AddUserAsync();
        var sweeper = app.Services.GetRequiredService<ExpirySweeper>();
        var old = await app.UserSaysAsync(botId, user, "old");

        app.Time.Advance(TimeSpan.FromSeconds(30));
        var young = await app.UserSaysAsync(botId, user, "young");
        app.Time.Advance(TimeSpan.FromSeconds(30));
        await sweeper.SweepOnceAsync();

        Assert.Equal(DeliveryState.NotDelivered, await app.DeliveryOfAsync(old));
        Assert.Equal(DeliveryState.Pending, await app.DeliveryOfAsync(young));
    }

    [Fact]
    [Trait("Rule", "Q-05")]
    public async Task Q05_second_poll_replaces_the_first_which_gets_409()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, token) = await app.AddBotAsync();
        var bot = app.BotClient(token);

        var first = bot.GetAsync("/v1/updates?timeout=50", TestContext.Current.CancellationToken);
        await app.Queue().PollWaitsAsync();
        var second = bot.GetAsync("/v1/updates?timeout=50", TestContext.Current.CancellationToken);

        var replaced = await first.WaitAsync(Soon, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, replaced.StatusCode);
        Assert.Equal("conflict", await replaced.ErrorCodeAsync());

        await app.Queue().PollWaitsAsync();
        await app.UserSaysAsync(botId, await app.AddUserAsync(), "hello");
        var updates = await (await second.WaitAsync(Soon, TestContext.Current.CancellationToken)).UpdatesAsync();
        Assert.Equal("hello", Assert.Single(updates).Text());
    }

    [Fact]
    [Trait("Rule", "Q-05")]
    public async Task Q05_polls_of_different_bots_do_not_replace_each_other()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (firstBot, firstToken) = await app.AddBotAsync("first");
        var (_, secondToken) = await app.AddBotAsync("second");

        var first = app.BotClient(firstToken).GetAsync("/v1/updates?timeout=50", TestContext.Current.CancellationToken);
        await app.Queue().PollWaitsAsync();
        var second = app.BotClient(secondToken).GetAsync("/v1/updates?timeout=50", TestContext.Current.CancellationToken);
        await app.Queue().PollWaitsAsync();

        await app.UserSaysAsync(firstBot, await app.AddUserAsync(), "hello");
        var updates = await (await first.WaitAsync(Soon, TestContext.Current.CancellationToken)).UpdatesAsync();
        Assert.Single(updates);
        Assert.False(second.IsCompleted);
    }

    [Fact]
    [Trait("Rule", "Q-06")]
    public async Task Q06_waiting_poll_returns_when_a_message_arrives()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, token) = await app.AddBotAsync();
        var user = await app.AddUserAsync();

        var poll = app.BotClient(token).GetAsync("/v1/updates?timeout=50", TestContext.Current.CancellationToken);
        await app.Queue().PollWaitsAsync();
        Assert.False(poll.IsCompleted);
        await app.UserSaysAsync(botId, user, "hello");

        var updates = await (await poll.WaitAsync(Soon, TestContext.Current.CancellationToken)).UpdatesAsync();
        Assert.Equal("hello", Assert.Single(updates).Text());
    }

    [Fact]
    [Trait("Rule", "Q-06")]
    public async Task Q06_poll_returns_an_empty_list_when_the_timeout_passes()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (_, token) = await app.AddBotAsync();

        var poll = app.BotClient(token).GetAsync("/v1/updates?timeout=20", TestContext.Current.CancellationToken);
        await app.Queue().PollWaitsAsync();
        app.Time.Advance(TimeSpan.FromSeconds(19));
        Assert.False(poll.IsCompleted);
        app.Time.Advance(TimeSpan.FromSeconds(1));

        Assert.Empty(await (await poll.WaitAsync(Soon, TestContext.Current.CancellationToken)).UpdatesAsync());
    }

    [Fact]
    [Trait("Rule", "Q-06")]
    public async Task Q06_timeout_zero_returns_at_once()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (_, token) = await app.AddBotAsync();

        Assert.Empty(await app.BotClient(token).UpdatesAsync("timeout=0"));
    }

    [Fact]
    [Trait("Rule", "Q-06")]
    public async Task Q06_poll_with_a_waiting_update_returns_at_once()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, token) = await app.AddBotAsync();
        await app.UserSaysAsync(botId, await app.AddUserAsync(), "hello");

        // The clock of the test never moves, so only "at once" can end this call.
        Assert.Single(await app.BotClient(token).UpdatesAsync("timeout=50"));
    }

    [Fact]
    [Trait("Rule", "Q-08")]
    public async Task Q08_limit_cuts_the_batch_and_the_rest_comes_next()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, token) = await app.AddBotAsync();
        var bot = app.BotClient(token);
        foreach (var name in new[] { "a", "b", "c" })
            await app.UserSaysAsync(botId, await app.AddUserAsync(name), name);

        var firstTwo = await bot.UpdatesAsync("timeout=0&limit=2");
        var rest = await bot.UpdatesAsync($"timeout=0&offset={firstTwo[^1].Id() + 1}");

        Assert.Equal(["a", "b"], firstTwo.Select(u => u.Text()));
        Assert.Equal("c", Assert.Single(rest).Text());
    }

    [Fact]
    [Trait("Rule", "Q-09")]
    public async Task Q09_waiting_poll_returns_empty_when_the_server_stops()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (_, token) = await app.AddBotAsync();

        var poll = app.BotClient(token).GetAsync("/v1/updates?timeout=50", TestContext.Current.CancellationToken);
        await app.Queue().PollWaitsAsync();
        app.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();

        Assert.Empty(await (await poll.WaitAsync(Soon, TestContext.Current.CancellationToken)).UpdatesAsync());
    }
}
