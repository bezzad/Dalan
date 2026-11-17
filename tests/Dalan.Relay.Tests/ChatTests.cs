using Dalan.Relay.Chats;
using Dalan.Relay.Data;
using Dalan.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Relay.Tests;

/// <summary>What happens when a user writes to a bot.</summary>
public class ChatTests(PostgresServer postgres)
{
    private static Task<(ChatError Error, Message? Message)> ReceiveAsync(DalanApp app, long botId, long userId, string text) =>
        app.Services.GetRequiredService<ChatService>()
            .ReceiveFromUserAsync(botId, userId, text, null, TestContext.Current.CancellationToken);

    [Fact]
    [Trait("Rule", "Q-01")]
    public async Task Q01_message_is_saved_as_pending_and_the_bot_gets_it()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, token) = await app.AddBotAsync();
        var user = await app.AddUserAsync();

        var message = await app.UserSaysAsync(botId, user, "/start");

        var stored = Assert.Single(await app.MessagesAsync());
        Assert.Equal(DeliveryState.Pending, stored.Delivery);
        Assert.Equal(Direction.FromUser, stored.Direction);
        Assert.Equal("/start", stored.Content.Text);
        Assert.Equal(message.Id, Assert.Single(await app.BotClient(token).UpdatesAsync()).Id());
    }

    [Fact]
    [Trait("Rule", "REL-05")]
    public async Task REL05_first_message_creates_an_active_membership()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, _) = await app.AddBotAsync();
        var user = await app.AddUserAsync();

        await app.UserSaysAsync(botId, user, "/start");
        await app.UserSaysAsync(botId, user, "again");

        await using var db = await app.Services.GetRequiredService<IDbContextFactory<RelayDb>>()
            .CreateDbContextAsync(TestContext.Current.CancellationToken);
        var membership = Assert.Single(await db.Memberships.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal((botId, user, MembershipState.Active), (membership.BotId, membership.UserId, membership.State));
    }

    [Fact]
    [Trait("Rule", "REL-05")]
    public async Task REL05_message_from_an_unknown_user_is_refused()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, _) = await app.AddBotAsync();

        var (error, _) = await ReceiveAsync(app, botId, userId: 404, "hello");

        Assert.Equal(ChatError.UnknownUser, error);
        Assert.Empty(await app.MessagesAsync());
    }

    [Fact]
    [Trait("Rule", "REL-06")]
    public async Task REL06_user_and_bot_share_one_sequence_in_a_chat()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, token) = await app.AddBotAsync();
        var user = await app.AddUserAsync();

        await app.UserSaysAsync(botId, user, "one");
        await app.BotClient(token).SendAsync(user, "two");
        await app.UserSaysAsync(botId, user, "three");

        Assert.Equal([1, 2, 3], (await app.MessagesAsync()).Select(m => m.Seq));
    }

    [Fact]
    [Trait("Rule", "REL-06")]
    public async Task REL06_messages_at_the_same_moment_get_different_numbers()
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Relay:LatestCommandWins", "false"));
        var (botId, _) = await app.AddBotAsync();
        var user = await app.AddUserAsync();

        await Task.WhenAll(Enumerable.Range(1, 20).Select(i => app.UserSaysAsync(botId, user, $"message {i}")));

        Assert.Equal(Enumerable.Range(1, 20).Select(i => (long)i), (await app.MessagesAsync()).Select(m => m.Seq).Order());
    }

    [Fact]
    [Trait("Rule", "REL-06")]
    public async Task REL06_each_chat_starts_at_one()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, _) = await app.AddBotAsync();

        var first = await app.UserSaysAsync(botId, await app.AddUserAsync("Sara"), "hello");
        var second = await app.UserSaysAsync(botId, await app.AddUserAsync("Omid"), "hello");

        Assert.Equal((1, 1), (first.Seq, second.Seq));
    }

    [Theory]
    [Trait("Rule", "REL-09")]
    [InlineData(0, ChatError.TextEmpty)]
    [InlineData(11, ChatError.TextTooLong)]
    public async Task REL09_user_text_must_fit_the_limit(int length, ChatError expected)
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Relay:MaxTextLength", "10"));
        var (botId, _) = await app.AddBotAsync();
        var user = await app.AddUserAsync();

        var (error, _) = await ReceiveAsync(app, botId, user, new string('x', length));

        Assert.Equal(expected, error);
        Assert.Empty(await app.MessagesAsync());
    }

    [Fact]
    [Trait("Rule", "REL-09")]
    public async Task REL09_user_text_at_the_limit_is_accepted()
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Relay:MaxTextLength", "10"));
        var (botId, _) = await app.AddBotAsync();

        var (error, _) = await ReceiveAsync(app, botId, await app.AddUserAsync(), new string('x', 10));

        Assert.Equal(ChatError.None, error);
    }

    [Fact]
    [Trait("Rule", "Q-04")]
    public async Task Q04_only_the_last_of_three_messages_reaches_the_bot()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, token) = await app.AddBotAsync();
        var user = await app.AddUserAsync();

        var first = await app.UserSaysAsync(botId, user, "one");
        var second = await app.UserSaysAsync(botId, user, "two");
        await app.UserSaysAsync(botId, user, "three");

        Assert.Equal("three", Assert.Single(await app.BotClient(token).UpdatesAsync()).Text());
        Assert.Equal(DeliveryState.NotDelivered, await app.DeliveryOfAsync(first));
        Assert.Equal(DeliveryState.NotDelivered, await app.DeliveryOfAsync(second));
    }

    [Fact]
    [Trait("Rule", "Q-04")]
    public async Task Q04_setting_off_gives_the_bot_all_three()
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Relay:LatestCommandWins", "false"));
        var (botId, token) = await app.AddBotAsync();
        var user = await app.AddUserAsync();

        foreach (var text in new[] { "one", "two", "three" }) await app.UserSaysAsync(botId, user, text);

        Assert.Equal(["one", "two", "three"], (await app.BotClient(token).UpdatesAsync()).Select(u => u.Text()));
    }
}
