using Dalan.Relay.Chats;
using Dalan.Testing;

namespace Dalan.Relay.Tests;

public class RestartTests(PostgresServer postgres)
{
    [Fact]
    [Trait("Rule", "Q-07")]
    public async Task Q07_waiting_message_is_back_in_the_queue_after_a_restart()
    {
        var database = await postgres.NewDatabaseAsync();
        string token;
        Message message;
        await using (var before = RelayApp.Create(database))
        {
            await before.ReadyAsync();
            (var botId, token) = await before.AddBotAsync();
            message = await before.UserSaysAsync(botId, await before.AddUserAsync("Sara"), "hello");
        }

        await using var after = RelayApp.Create(database);
        after.Time.Advance(TimeSpan.FromSeconds(59));
        await after.ReadyAsync();

        var update = Assert.Single(await after.BotClient(token).UpdatesAsync());
        Assert.Equal(message.Id, update.Id());
        Assert.Equal("hello", update.Text());
        Assert.Equal("Sara", update.GetProperty("message").GetProperty("from").GetProperty("name").GetString());
    }

    [Fact]
    [Trait("Rule", "Q-07")]
    public async Task Q07_message_that_expired_during_the_restart_is_marked_not_delivered()
    {
        var database = await postgres.NewDatabaseAsync();
        string token;
        Message message;
        await using (var before = RelayApp.Create(database))
        {
            await before.ReadyAsync();
            (var botId, token) = await before.AddBotAsync();
            message = await before.UserSaysAsync(botId, await before.AddUserAsync(), "hello");
        }

        await using var after = RelayApp.Create(database);
        after.Time.Advance(TimeSpan.FromSeconds(60));
        await after.ReadyAsync();

        Assert.Equal(DeliveryState.NotDelivered, await after.DeliveryOfAsync(message));
        Assert.Empty(await after.BotClient(token).UpdatesAsync());
    }

    [Fact]
    [Trait("Rule", "Q-07")]
    public async Task Q07_delivered_message_is_not_sent_again_after_a_restart()
    {
        var database = await postgres.NewDatabaseAsync();
        string token;
        await using (var before = RelayApp.Create(database))
        {
            await before.ReadyAsync();
            (var botId, token) = await before.AddBotAsync();
            await before.UserSaysAsync(botId, await before.AddUserAsync(), "hello");
            var bot = before.BotClient(token);
            var update = Assert.Single(await bot.UpdatesAsync());
            await bot.UpdatesAsync($"timeout=0&offset={update.Id() + 1}");
        }

        await using var after = RelayApp.Create(database);
        await after.ReadyAsync();

        Assert.Empty(await after.BotClient(token).UpdatesAsync());
    }
}
