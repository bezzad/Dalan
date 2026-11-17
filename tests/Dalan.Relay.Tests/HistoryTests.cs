using Dalan.Relay.Chats;
using Dalan.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Relay.Tests;

public class HistoryTests(PostgresServer postgres)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    /// <summary>The "/start" of the chat reached the bot, so it is no longer waiting.</summary>
    private static Task DeliveredAsync(DalanApp app) =>
        PostgresServer.ExecuteAsync(app.ConnectionString, "UPDATE relay.messages SET delivery = 1 WHERE delivery = 0");

    private static Task SweepAsync(DalanApp app) => app.Services.GetRequiredService<HistorySweeper>().SweepOnceAsync(Cancel);

    private static async Task<long[]> SeqsAsync(DalanApp app) => [.. (await app.MessagesAsync()).Select(m => m.Seq).Order()];

    [Fact]
    [Trait("Rule", "REL-44")]
    public async Task REL44_only_the_newest_messages_stay_and_numbers_keep_counting()
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Relay:HistoryMessages", "3"));
        var chat = await Chat.StartAsync(app);
        await DeliveredAsync(app);
        for (var i = 2; i <= 5; i++) await chat.BotSaysAsync($"message {i}");

        await SweepAsync(app);

        Assert.Equal(new long[] { 3, 4, 5 }, await SeqsAsync(app));
        Assert.Equal(new long[] { 3, 4, 5 }, (await chat.Browser.HistoryAsync(chat.BotId)).Ids());
        Assert.Equal(6, await chat.BotSaysAsync("message 6"));
    }

    [Fact]
    [Trait("Rule", "REL-44")]
    public async Task REL44_messages_older_than_the_days_go()
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Relay:HistoryDays", "30"));
        var chat = await Chat.StartAsync(app);
        await DeliveredAsync(app);
        app.Time.Advance(TimeSpan.FromDays(31));
        await chat.BotSaysAsync("today");

        await SweepAsync(app);

        Assert.Equal(new long[] { 2 }, await SeqsAsync(app));
    }

    [Fact]
    [Trait("Rule", "REL-44")]
    public async Task REL44_a_message_that_waits_for_the_bot_stays()
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Relay:HistoryMessages", "1"));
        var chat = await Chat.StartAsync(app);
        await chat.BotSaysAsync("newer");

        await SweepAsync(app);

        var messages = await app.MessagesAsync();
        Assert.Equal(new long[] { 1, 2 }, messages.Select(m => m.Seq).Order());
        Assert.Equal(DeliveryState.Pending, messages.Single(m => m.Seq == 1).Delivery);
    }
}
