using System.Net;
using Dalan.Relay.Chats;
using Dalan.Relay.ClientApi;
using Dalan.Relay.Live;
using Dalan.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dalan.Relay.Tests;

public class LiveTests(PostgresServer postgres)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("Rule", "LIVE-01")]
    public async Task LIVE01_stream_is_server_sent_events_and_stays_open()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (browser, _) = await app.LoginAsync();

        // OpenAsync checks the status and the content type, and reads the first line.
        using var stream = await EventStreamReader.OpenAsync(browser);

        Assert.True(await stream.IsQuietAsync(), "the stream ended or sent something with no message");
    }

    [Fact]
    [Trait("Rule", "LIVE-02")]
    public async Task LIVE02_bot_answer_arrives_live()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, token) = await app.AddBotAsync();
        var (browser, userId) = await app.LoginAsync();
        using var stream = await EventStreamReader.OpenAsync(browser);
        await browser.SayAsync(botId, "/start");
        await stream.NextMessageAsync("/start");

        await app.BotClient(token).SendAsync(userId, "سلام", [["Yes", "No"]]);

        var data = await stream.NextMessageAsync();
        Assert.Equal(botId, data.GetProperty("bot_id").GetInt64());
        var message = data.GetProperty("message");
        Assert.Equal(2, message.GetProperty("id").GetInt64());
        Assert.Equal("bot", message.GetProperty("from").GetString());
        Assert.Equal("سلام", message.GetProperty("text").GetString());
        Assert.Equal(app.Time.GetUtcNow(), message.GetProperty("date").GetDateTimeOffset());
        Assert.Equal("Yes", message.GetProperty("reply_keyboard")[0][0].GetString());
    }

    [Fact]
    [Trait("Rule", "LIVE-02")]
    public async Task LIVE02_own_message_from_another_device_arrives_live()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, _) = await app.AddBotAsync();
        var (phone, _) = await app.LoginAsync(phone: "09121234567");
        var (laptop, _) = await app.LoginAsync(phone: "09121234567");
        using var stream = await EventStreamReader.OpenAsync(laptop);

        await phone.SayAsync(botId, "/start");

        var message = (await stream.NextMessageAsync()).GetProperty("message");
        Assert.Equal("user", message.GetProperty("from").GetString());
        Assert.Equal("/start", message.GetProperty("text").GetString());
        Assert.False(message.TryGetProperty("reply_keyboard", out _));
    }

    [Fact]
    [Trait("Rule", "LIVE-03")]
    public async Task LIVE03_another_user_gets_nothing()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, token) = await app.AddBotAsync();
        var (alice, aliceId) = await app.LoginAsync("Alice");
        var (bob, _) = await app.LoginAsync("Bob");
        using var aliceStream = await EventStreamReader.OpenAsync(alice);
        using var bobStream = await EventStreamReader.OpenAsync(bob);
        await alice.SayAsync(botId, "/start");
        await bob.SayAsync(botId, "/start");
        await aliceStream.NextMessageAsync("/start");
        await bobStream.NextMessageAsync("/start");

        await app.BotClient(token).SendAsync(aliceId, "only for alice");

        await aliceStream.NextMessageAsync("only for alice");
        Assert.True(await bobStream.IsQuietAsync(), "bob got a message of alice's chat");
    }

    /// <summary>A notifier that cannot pass anything on, so the worker never finishes an event.</summary>
    private sealed class BrokenNotifier : IMessageNotifier
    {
        public void Publish(LiveEvent liveEvent) => throw new InvalidOperationException("the notifier is down");

        public IMessageSubscription Subscribe(long userId) => throw new NotSupportedException();
    }

    [Fact]
    [Trait("Rule", "LIVE-04")]
    public async Task LIVE04_event_is_saved_with_its_message_and_stays_until_it_is_passed_on()
    {
        await using var app = new DalanApp(await postgres.NewDatabaseAsync(), configure: web =>
            web.ConfigureServices(services => services.AddSingleton<IMessageNotifier, BrokenNotifier>()));
        await app.ReadyAsync();
        var (botId, _) = await app.AddBotAsync();
        var (browser, _) = await app.LoginAsync();

        await browser.SayAsync(botId, "/start");

        Assert.Equal(1L, await PostgresServer.ScalarAsync<long>(app.ConnectionString,
            "SELECT count(*) FROM relay.outbox WHERE type = 'MessageChanged' AND payload->'Message'->>'Text' = '/start'"));
    }

    [Fact]
    [Trait("Rule", "LIVE-04")]
    public async Task LIVE04_event_is_removed_after_it_is_passed_on()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, _) = await app.AddBotAsync();
        var (browser, _) = await app.LoginAsync();
        using var stream = await EventStreamReader.OpenAsync(browser);

        await browser.SayAsync(botId, "/start");
        await stream.NextMessageAsync();

        // The worker commits the delete right after it passed the event on.
        var left = 1L;
        for (var i = 0; i < 100 && left > 0; i++)
        {
            await Task.Delay(50, Cancel);
            left = await PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT count(*) FROM relay.outbox");
        }

        Assert.Equal(0, left);
    }

    [Fact]
    [Trait("Rule", "LIVE-04")]
    public async Task LIVE04_left_over_event_is_passed_on_at_the_next_poll()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (browser, userId) = await app.LoginAsync();
        using var stream = await EventStreamReader.OpenAsync(browser);

        // An event that nobody announced to the worker, as after a crash between commit and wake.
        await PostgresServer.ExecuteAsync(app.ConnectionString, $$$"""
            INSERT INTO relay.outbox (type, payload) VALUES ('MessageChanged',
                '{"BotId":1,"UserId":{{{userId}}},"Message":{"Id":9,"Version":9,"From":"bot","Text":"late","Date":"2026-11-16T12:00:00+00:00"}}')
            """);

        string? block;
        do
        {
            block = await stream.NextAfterAsync(app, TimeSpan.FromSeconds(5));
        }
        while (block?.StartsWith(':') == true);

        Assert.Contains("\"text\":\"late\"", block, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "LIVE-05")]
    public async Task LIVE05_quiet_stream_gets_a_heartbeat()
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Relay:StreamHeartbeatSeconds", "20"));
        var (browser, _) = await app.LoginAsync();
        using var stream = await EventStreamReader.OpenAsync(browser);

        Assert.True(await stream.IsQuietAsync(), "a heartbeat came before its time");
        Assert.Equal(": ping", await stream.NextAfterAsync(app, TimeSpan.FromSeconds(20)));
    }

    [Fact]
    [Trait("Rule", "LIVE-06")]
    public async Task LIVE06_stream_over_the_limit_closes_the_oldest()
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Relay:MaxStreamsPerUser", "2"));
        var (botId, _) = await app.AddBotAsync();
        var (browser, _) = await app.LoginAsync();
        using var first = await EventStreamReader.OpenAsync(browser);
        using var second = await EventStreamReader.OpenAsync(browser);

        using var third = await EventStreamReader.OpenAsync(browser);

        Assert.Null(await first.NextAsync());
        await browser.SayAsync(botId, "/start");
        Assert.Equal("/start", (await second.NextMessageAsync()).GetProperty("message").GetProperty("text").GetString());
        Assert.Equal("/start", (await third.NextMessageAsync()).GetProperty("message").GetProperty("text").GetString());
    }

    [Fact]
    [Trait("Rule", "LIVE-06")]
    public async Task LIVE06_limit_is_per_user()
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Relay:MaxStreamsPerUser", "1"));
        var (alice, _) = await app.LoginAsync("Alice");
        var (bob, _) = await app.LoginAsync("Bob");
        using var aliceStream = await EventStreamReader.OpenAsync(alice);

        using var bobStream = await EventStreamReader.OpenAsync(bob);

        Assert.True(await aliceStream.IsQuietAsync(), "bob's stream closed alice's");
    }

    [Fact]
    [Trait("Rule", "LIVE-07")]
    public async Task LIVE07_stream_ends_when_the_session_expires()
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Identity:AccessTokenMinutes", "15"), ("Relay:StreamHeartbeatSeconds", "3600"));
        var (browser, _) = await app.LoginAsync();
        using var stream = await EventStreamReader.OpenAsync(browser);

        app.Time.Advance(TimeSpan.FromMinutes(14));
        Assert.True(await stream.IsQuietAsync(), "the stream ended before the session did");
        app.Time.Advance(TimeSpan.FromMinutes(1));

        Assert.Null(await stream.NextAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/events", Cancel)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "LIVE-07")]
    public async Task LIVE07_stream_ends_when_the_server_stops()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (browser, _) = await app.LoginAsync();
        using var stream = await EventStreamReader.OpenAsync(browser);

        app.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();

        Assert.Null(await stream.NextAsync());
    }

    private static long Seq(LiveEvent liveEvent) => ((MessageChanged)liveEvent).Message.Id;

    private static MessageChanged Event(long userId, long seq) =>
        new(BotId: 1, userId, new ChatMessageJson(seq, seq, "bot", $"text {seq}", DateTimeOffset.UnixEpoch));

    [Fact]
    [Trait("Rule", "LIVE-08")]
    public async Task LIVE08_reader_that_does_not_keep_up_is_cut_off()
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Relay:StreamBufferSize", "2"));
        var notifier = app.Services.GetRequiredService<IMessageNotifier>();
        using var slow = notifier.Subscribe(userId: 7);
        using var other = notifier.Subscribe(userId: 8);

        for (var seq = 1; seq <= 3; seq++) notifier.Publish(Event(userId: 7, seq));
        notifier.Publish(Event(userId: 8, seq: 1));

        // What fitted in the buffer can still be read; then the stream is over.
        Assert.True(slow.Events.TryRead(out var first) && Seq(first) == 1);
        Assert.True(slow.Events.TryRead(out var second) && Seq(second) == 2);
        Assert.False(await slow.Events.WaitToReadAsync(Cancel));
        Assert.True(other.Events.TryRead(out _));
        Assert.False(other.Events.Completion.IsCompleted);
    }

    [Fact]
    [Trait("Rule", "LIVE-08")]
    public async Task LIVE08_closed_stream_gets_no_more_events_and_a_new_one_works()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var notifier = app.Services.GetRequiredService<IMessageNotifier>();
        var gone = notifier.Subscribe(userId: 7);
        gone.Dispose();

        notifier.Publish(Event(userId: 7, seq: 1));
        using var again = notifier.Subscribe(userId: 7);
        notifier.Publish(Event(userId: 7, seq: 2));

        Assert.False(gone.Events.TryRead(out _));
        Assert.True(again.Events.TryRead(out var message) && Seq(message) == 2);
    }
}
