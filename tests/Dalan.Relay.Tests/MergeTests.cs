using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Identity.Contracts;
using Dalan.Relay.Chats;
using Dalan.Relay.Data;
using Dalan.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Relay.Tests;

/// <summary>What Relay does when Identity merges two accounts (REL-55, REL-56).</summary>
public class MergeTests(PostgresServer postgres)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static Task MergeAsync(DalanApp app, long fromUserId, long intoUserId) =>
        app.Services.GetServices<IUserMergeListener>().Single().UserMergedAsync(fromUserId, intoUserId, Cancel);

    private static async Task<string[]> TextsAsync(HttpClient browser, long botId) =>
        [.. (await browser.HistoryAsync(botId)).Select(m => m.GetProperty("text").GetString()!)];

    // ---- REL-55 ----

    [Fact]
    [Trait("Rule", "REL-55")]
    public async Task REL55_a_chat_only_the_gone_user_had_moves_with_its_ids()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        await chat.BotSaysAsync("hello");
        var (kept, keptId) = await app.LoginAsync("Sara phone");

        await MergeAsync(app, chat.UserId, keptId);

        var ids = (await kept.HistoryAsync(chat.BotId)).Ids();
        Assert.Equal([1L, 2L], ids);
        Assert.Equal(["/start", "hello"], await TextsAsync(kept, chat.BotId));
    }

    [Fact]
    [Trait("Rule", "REL-55")]
    public async Task REL55_two_chats_with_one_bot_become_one_in_time_order()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, _) = await app.AddBotAsync();
        var (kept, keptId) = await app.LoginAsync("Kept");
        var (gone, goneId) = await app.LoginAsync("Gone");
        foreach (var (browser, text) in new[] { (kept, "a"), (gone, "b"), (kept, "c"), (gone, "d"), (kept, "e") })
        {
            Assert.Equal(HttpStatusCode.OK, (await browser.SayAsync(botId, text)).StatusCode);
            app.Time.Advance(TimeSpan.FromSeconds(1));
        }

        await MergeAsync(app, goneId, keptId);

        var ids = (await kept.HistoryAsync(botId)).Ids();
        Assert.Equal([4L, 5L, 6L, 7L, 8L], ids);
        Assert.Equal(["a", "b", "c", "d", "e"], await TextsAsync(kept, botId));
    }

    [Fact]
    [Trait("Rule", "REL-55")]
    public async Task REL55_the_read_position_follows_the_new_ids_and_no_bot_changes_owner()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, _) = await app.AddBotAsync();
        var (kept, keptId) = await app.LoginAsync("Kept");
        var (gone, goneId) = await app.LoginAsync("Gone");
        await kept.SayAsync(botId, "a");
        app.Time.Advance(TimeSpan.FromSeconds(1));
        await gone.SayAsync(botId, "b");
        await app.Services.GetRequiredService<ChatService>().ReadAsync(botId, keptId, 1, Cancel);

        await MergeAsync(app, goneId, keptId);

        await using var db = await app.Services.GetRequiredService<IDbContextFactory<RelayDb>>().CreateDbContextAsync(Cancel);
        var membership = await db.Memberships.SingleAsync(m => m.BotId == botId, Cancel);
        Assert.Equal(keptId, membership.UserId);
        // Kept read "a" (now 2); the gone side's own "b" counts as read there (now 3): what either side read stays read.
        Assert.Equal(3L, membership.LastReadSeq);
        Assert.Equal(3L, membership.LastSeq);
        Assert.Equal(1L, (await db.Bots.SingleAsync(b => b.Id == botId, Cancel)).OwnerUserId);
    }

    [Fact]
    [Trait("Rule", "REL-55")]
    public async Task REL55_push_subscriptions_move_and_the_newest_stay()
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Notification:Push:MaxSubscriptionsPerUser", "2"));
        var (_, keptId) = await app.LoginAsync("Kept");
        var (_, goneId) = await app.LoginAsync("Gone");
        await PostgresServer.ExecuteAsync(app.ConnectionString, $"""
            INSERT INTO notification.push_subscriptions (user_id, endpoint, p256dh, auth, created_at) VALUES
            ({keptId}, 'https://push.test/old', 'k', 'a', now() - interval '3 days'),
            ({goneId}, 'https://push.test/mid', 'k', 'a', now() - interval '2 days'),
            ({goneId}, 'https://push.test/new', 'k', 'a', now() - interval '1 day')
            """);

        await MergeAsync(app, goneId, keptId);

        var endpoints = await PostgresServer.ScalarAsync<string[]>(app.ConnectionString,
            $"SELECT array_agg(endpoint ORDER BY endpoint) FROM notification.push_subscriptions WHERE user_id = {keptId}");
        Assert.Equal(["https://push.test/mid", "https://push.test/new"], endpoints);
    }

    // ---- REL-56 ----

    [Fact]
    [Trait("Rule", "REL-56")]
    public async Task REL56_the_bot_gets_one_chat_moved_update()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var (_, keptId) = await app.LoginAsync("Sara phone");

        await MergeAsync(app, chat.UserId, keptId);

        var moved = (await chat.Bot.UpdatesAsync()).Where(u => u.TryGetProperty("chat_moved", out _)).ToList();
        var single = Assert.Single(moved).GetProperty("chat_moved");
        Assert.Equal(chat.UserId, single.GetProperty("from_user_id").GetInt64());
        Assert.Equal(keptId, single.GetProperty("to_user_id").GetInt64());
    }

    [Fact]
    [Trait("Rule", "REL-56")]
    public async Task REL56_a_send_to_the_old_chat_names_the_new_one_for_a_while()
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Relay:MovedChatDays", "180"));
        var chat = await Chat.StartAsync(app);
        var (_, keptId) = await app.LoginAsync("Sara phone");
        await MergeAsync(app, chat.UserId, keptId);

        var response = await chat.Bot.SendAsync(chat.UserId, "hi");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancel);
        app.Time.Advance(TimeSpan.FromDays(181));
        var later = await chat.Bot.SendAsync(chat.UserId, "hi");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("chat_moved", body.GetProperty("error").GetString());
        Assert.Equal(keptId, body.GetProperty("to_user_id").GetInt64());
        Assert.Equal("chat_not_active", await later.ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.OK, (await chat.Bot.SendAsync(keptId, "hi")).StatusCode);
    }
}
