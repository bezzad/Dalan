using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Relay.Queue;
using Dalan.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Relay.Tests;

/// <summary>Milestone 7: "not delivered" marks, unread counts and bot status.</summary>
public class PresenceTests(PostgresServer postgres)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static bool NotDelivered(JsonElement message) =>
        message.TryGetProperty("not_delivered", out var value) && value.GetBoolean();

    private static Task<HttpResponseMessage> ReadAsync(Chat chat, object body) =>
        chat.Browser.PostAsJsonAsync($"/api/bots/{chat.BotId}/read", body, Cancel);

    private static async Task<int> UnreadAfterReadAsync(Chat chat, long messageId)
    {
        var response = await ReadAsync(chat, new { message_id = messageId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Cancel)).GetProperty("unread").GetInt32();
    }

    private static async Task<JsonElement> ListedAsync(Chat chat) =>
        Assert.Single((await chat.Browser.JsonAsync("/api/bots")).GetProperty("bots").EnumerateArray());

    private static Task<JsonElement> StatusAsync(Chat chat) => chat.Browser.JsonAsync($"/api/bots/{chat.BotId}/status");

    [Fact]
    [Trait("Rule", "REL-31")]
    [Trait("Rule", "Q-02")]
    public async Task REL31_expired_message_gets_a_new_version_an_event_and_the_mark()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        using var stream = await EventStreamReader.OpenAsync(chat.Browser);

        app.Time.Advance(TimeSpan.FromSeconds(61));
        await app.Services.GetRequiredService<ExpirySweeper>().SweepOnceAsync();

        // The event of the new message itself may still come first.
        JsonElement marked;
        do marked = await stream.NextMessageAsync("/start");
        while (marked.GetProperty("version").GetInt64() < 2);
        Assert.True(NotDelivered(marked));
        Assert.Equal(2, marked.GetProperty("version").GetInt64());
        var synced = Assert.Single(await chat.Browser.HistoryAsync(chat.BotId, "after=1"));
        Assert.True(NotDelivered(synced));
    }

    [Fact]
    [Trait("Rule", "REL-31")]
    [Trait("Rule", "Q-04")]
    public async Task REL31_replaced_command_is_marked_and_the_newer_is_not()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);

        await chat.Browser.SayAsync(chat.BotId, "second");

        Assert.True(NotDelivered(await chat.MessageAsync(1)));
        Assert.False(NotDelivered(await chat.MessageAsync(2)));
    }

    [Fact]
    [Trait("Rule", "REL-31")]
    [Trait("Rule", "Q-03")]
    public async Task REL31_delivered_message_keeps_its_version()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);

        var update = Assert.Single(await chat.Bot.UpdatesAsync());
        await chat.Bot.UpdatesAsync($"offset={update.Id() + 1}&timeout=0");

        var message = await chat.MessageAsync(1);
        Assert.Equal(1, message.GetProperty("version").GetInt64());
        Assert.False(NotDelivered(message));
    }

    [Fact]
    [Trait("Rule", "REL-31")]
    [Trait("Rule", "Q-07")]
    public async Task REL31_message_found_expired_at_start_is_marked_for_the_user()
    {
        var database = await postgres.NewDatabaseAsync();
        long botId;
        await using (var before = RelayApp.Create(database))
        {
            await before.ReadyAsync();
            botId = (await Chat.StartAsync(before)).BotId;
        }

        await using var after = RelayApp.Create(database);
        after.Time.Advance(TimeSpan.FromSeconds(60));
        await after.ReadyAsync();

        var version = await PostgresServer.ScalarAsync<long>(after.ConnectionString,
            $"SELECT version FROM relay.messages WHERE bot_id = {botId} AND seq = 1");
        Assert.Equal(2, version);
    }

    [Fact]
    [Trait("Rule", "REL-14")]
    public async Task REL14_list_has_unread_and_status()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        await chat.BotSaysAsync("one");
        await chat.BotSaysAsync("two");

        var bot = await ListedAsync(chat);

        Assert.Equal(chat.BotId, bot.GetProperty("id").GetInt64());
        Assert.Equal(2, bot.GetProperty("unread").GetInt32());
        Assert.False(bot.GetProperty("online").GetBoolean());
        Assert.Equal(JsonValueKind.Null, bot.GetProperty("last_seen").ValueKind);
    }

    [Fact]
    [Trait("Rule", "REL-32")]
    public async Task REL32_read_up_to_a_message_leaves_the_newer_unread()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        foreach (var text in new[] { "a", "b", "c" }) await chat.BotSaysAsync(text);

        Assert.Equal(1, await UnreadAfterReadAsync(chat, 3));
        Assert.Equal(1, (await ListedAsync(chat)).GetProperty("unread").GetInt32());
    }

    [Fact]
    [Trait("Rule", "REL-32")]
    public async Task REL32_read_position_never_goes_back_or_past_the_newest()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        foreach (var text in new[] { "a", "b", "c" }) await chat.BotSaysAsync(text);

        Assert.Equal(0, await UnreadAfterReadAsync(chat, 4));
        Assert.Equal(0, await UnreadAfterReadAsync(chat, 2));
        Assert.Equal(0, await UnreadAfterReadAsync(chat, 99));
        await chat.BotSaysAsync("d");
        Assert.Equal(1, (await ListedAsync(chat)).GetProperty("unread").GetInt32());
    }

    [Fact]
    [Trait("Rule", "REL-32")]
    public async Task REL32_sending_reads_the_chat()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        await chat.BotSaysAsync("a");
        await chat.BotSaysAsync("b");

        await chat.Browser.SayAsync(chat.BotId, "ok");

        Assert.Equal(0, (await ListedAsync(chat)).GetProperty("unread").GetInt32());
    }

    [Fact]
    [Trait("Rule", "REL-32")]
    public async Task REL32_deleted_bot_message_is_not_unread()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var gone = await chat.BotSaysAsync("a");
        await chat.BotSaysAsync("b");

        await chat.DeleteAsync(gone);

        Assert.Equal(1, (await ListedAsync(chat)).GetProperty("unread").GetInt32());
    }

    [Fact]
    [Trait("Rule", "REL-32")]
    public async Task REL32_bad_body_or_unknown_chat()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var (otherBot, _) = await app.AddBotAsync("otherbot");

        var bad = await ReadAsync(chat, new { id = 1 });
        var unknown = await chat.Browser.PostAsJsonAsync($"/api/bots/{otherBot}/read", new { message_id = 1 }, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal("bad_request", await bad.ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("bot_not_found", await unknown.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "LIVE-10")]
    public async Task LIVE10_read_reaches_the_users_other_streams()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        await chat.BotSaysAsync("a");
        await chat.BotSaysAsync("b");
        using var laptop = await EventStreamReader.OpenAsync(chat.Browser);

        await UnreadAfterReadAsync(chat, 2);

        var block = await laptop.NextAsync();
        while (!block!.StartsWith("event: read", StringComparison.Ordinal)) block = await laptop.NextAsync();
        var data = JsonSerializer.Deserialize<JsonElement>(block.Split('\n')[1]["data: ".Length..]);
        Assert.Equal(chat.BotId, data.GetProperty("bot_id").GetInt64());
        Assert.Equal(1, data.GetProperty("unread").GetInt32());
    }

    [Fact]
    [Trait("Rule", "REL-33")]
    public async Task REL33_bot_is_online_while_it_polls()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        Assert.Single(await chat.Bot.UpdatesAsync());

        var poll = chat.Bot.GetAsync("/v1/updates?offset=999999&timeout=30", Cancel);
        await app.Queue().PollWaitsAsync();
        app.Time.Advance(TimeSpan.FromSeconds(25));

        Assert.True((await StatusAsync(chat)).GetProperty("online").GetBoolean());
        app.Time.Advance(TimeSpan.FromSeconds(10));
        await poll;
    }

    [Fact]
    [Trait("Rule", "REL-33")]
    public async Task REL33_quiet_bot_is_offline_with_its_last_seen_time()
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Relay:BotOnlineSeconds", "60"));
        var chat = await Chat.StartAsync(app);
        var seenAt = app.Time.GetUtcNow();
        await chat.Bot.UpdatesAsync();

        Assert.True((await StatusAsync(chat)).GetProperty("online").GetBoolean());
        app.Time.Advance(TimeSpan.FromMinutes(2));
        var status = await StatusAsync(chat);

        Assert.False(status.GetProperty("online").GetBoolean());
        Assert.Equal(seenAt, status.GetProperty("last_seen").GetDateTimeOffset());
        Assert.Equal(seenAt, (await ListedAsync(chat)).GetProperty("last_seen").GetDateTimeOffset());
    }

    [Fact]
    [Trait("Rule", "REL-33")]
    public async Task REL33_bot_that_never_polled()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);

        var status = await StatusAsync(chat);

        Assert.False(status.GetProperty("online").GetBoolean());
        Assert.Equal(JsonValueKind.Null, status.GetProperty("last_seen").ValueKind);
    }

    [Fact]
    [Trait("Rule", "REL-33")]
    public async Task REL33_last_seen_survives_a_restart()
    {
        var database = await postgres.NewDatabaseAsync();
        long botId;
        await using (var before = RelayApp.Create(database))
        {
            await before.ReadyAsync();
            var (id, token) = await before.AddBotAsync();
            botId = id;
            await before.BotClient(token).UpdatesAsync();
        }

        await using var after = RelayApp.Create(database);
        await after.ReadyAsync();
        var (browser, _) = await after.LoginAsync();

        var status = await browser.JsonAsync($"/api/bots/{botId}/status");
        Assert.NotEqual(JsonValueKind.Null, status.GetProperty("last_seen").ValueKind);
    }

    [Fact]
    [Trait("Rule", "REL-33")]
    public async Task REL33_status_of_an_unknown_bot_is_404()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (browser, _) = await app.LoginAsync();

        var response = await browser.GetAsync("/api/bots/999999/status", Cancel);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
