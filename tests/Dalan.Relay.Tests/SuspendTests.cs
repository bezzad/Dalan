using System.Net;
using System.Net.Http.Json;
using Dalan.Testing;

namespace Dalan.Relay.Tests;

public class SuspendTests(PostgresServer postgres)
{
    private const string AdminPhone = "09129990001";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private Task<DalanApp> StartAsync() => RelayApp.StartAsync(postgres, ("Identity:AdminPhones", AdminPhone));

    private static Task<HttpResponseMessage> SuspendBotAsync(HttpClient admin, long botId, bool suspended) =>
        admin.PostAsJsonAsync($"/api/admin/bots/{botId}/suspend", new { suspended }, Cancel);

    [Fact]
    [Trait("Rule", "AUTH-20")]
    public async Task AUTH20_open_live_stream_ends_when_the_user_is_suspended()
    {
        await using var app = await StartAsync();
        var (admin, _) = await app.LoginAsync("Owner", AdminPhone);
        var (browser, userId) = await app.LoginAsync();
        using var stream = await EventStreamReader.OpenAsync(browser);

        (await admin.PostAsJsonAsync($"/api/admin/users/{userId}/suspend", new { suspended = true }, Cancel)).EnsureSuccessStatusCode();

        Assert.Null(await stream.NextAsync());
    }

    [Fact]
    [Trait("Rule", "REL-38")]
    public async Task REL38_bot_call_gets_403_and_is_still_in_the_access_log()
    {
        await using var app = await StartAsync();
        var (admin, _) = await app.LoginAsync("Owner", AdminPhone);
        var (botId, token) = await app.AddBotAsync();
        (await SuspendBotAsync(admin, botId, true)).EnsureSuccessStatusCode();

        var response = await DalanApp.From(app.BotClient(token), "203.0.113.7:51000").GetAsync("/v1/updates?timeout=0", Cancel);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("bot_suspended", await response.ErrorCodeAsync());
        Assert.Contains($"Bot {botId} 203.0.113.7 51000", await app.AccessLogAsync());
    }

    [Fact]
    [Trait("Rule", "REL-38")]
    public async Task REL38_user_cannot_write_press_or_start()
    {
        await using var app = await StartAsync();
        var (admin, _) = await app.LoginAsync("Owner", AdminPhone);
        var chat = await Chat.StartAsync(app);
        var buttonMessage = await chat.BotSaysAsync("pick", new[] { new[] { new { text = "Yes", callback_data = "yes" } } });
        var (stranger, _) = await app.LoginAsync("Stranger");
        (await SuspendBotAsync(admin, chat.BotId, true)).EnsureSuccessStatusCode();
        var before = (await app.MessagesAsync()).Count;

        var text = await chat.Browser.SayAsync(chat.BotId, "hello");
        var press = await chat.PressAsync(buttonMessage, "yes");
        var start = await stranger.SayAsync(chat.BotId, "/start");

        foreach (var response in new[] { text, press, start })
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("bot_suspended", await response.ErrorCodeAsync());
        }

        Assert.Equal(before, (await app.MessagesAsync()).Count);
    }

    [Fact]
    [Trait("Rule", "REL-38")]
    public async Task REL38_bot_shows_as_suspended_to_users()
    {
        await using var app = await StartAsync();
        var (admin, _) = await app.LoginAsync("Owner", AdminPhone);
        var chat = await Chat.StartAsync(app);
        (await SuspendBotAsync(admin, chat.BotId, true)).EnsureSuccessStatusCode();

        var list = (await chat.Browser.JsonAsync("/api/bots")).GetProperty("bots")[0];
        var found = await chat.Browser.JsonAsync("/api/bots/by-username/testbot");

        Assert.True(list.GetProperty("suspended").GetBoolean());
        Assert.True(found.GetProperty("suspended").GetBoolean());
    }

    [Fact]
    [Trait("Rule", "REL-38")]
    public async Task REL38_unsuspend_lets_the_bot_work_again()
    {
        await using var app = await StartAsync();
        var (admin, _) = await app.LoginAsync("Owner", AdminPhone);
        var (botId, token) = await app.AddBotAsync();
        (await SuspendBotAsync(admin, botId, true)).EnsureSuccessStatusCode();

        (await SuspendBotAsync(admin, botId, false)).EnsureSuccessStatusCode();

        Assert.Empty(await app.BotClient(token).UpdatesAsync());
    }
}
