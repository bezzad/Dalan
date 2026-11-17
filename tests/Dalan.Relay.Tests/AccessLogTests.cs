using System.Net;
using Dalan.Admin.AccessLog;
using Dalan.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Relay.Tests;

public class AccessLogTests(PostgresServer postgres)
{
    /// <summary>The front proxy on this machine, as the server sees its connection.</summary>
    private const string Proxy = "127.0.0.1:40000";

    [Fact]
    [Trait("Rule", "ADM-01")]
    public async Task ADM01_bot_call_is_recorded_with_address_port_and_time()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, token) = await app.AddBotAsync();

        await Bot(app, token, "203.0.113.7:51000").UpdatesAsync();

        Assert.Equal([$"Bot {botId} 203.0.113.7 51000"], await app.AccessLogAsync());
        Assert.Equal(app.Time.GetUtcNow(), await PostgresServer.ScalarAsync<DateTime>(app.ConnectionString, "SELECT at FROM admin.access_log"));
    }

    [Fact]
    [Trait("Rule", "ADM-01")]
    public async Task ADM01_user_call_is_recorded()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (browser, userId) = await app.LoginAsync();
        DalanApp.From(browser, Proxy).DefaultRequestHeaders.Add("X-Forwarded-For", "198.51.100.9:52000");

        await browser.JsonAsync("/api/bots");

        Assert.Equal([$"User {userId} 198.51.100.9 52000"], await app.AccessLogAsync());
    }

    [Fact]
    [Trait("Rule", "ADM-01")]
    public async Task ADM01_wrong_token_writes_no_row()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        await app.AddBotAsync();

        var response = await Bot(app, "1:wrong", "203.0.113.7:51000").GetAsync("/v1/updates?timeout=0", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await app.AccessLogAsync());
    }

    [Fact]
    [Trait("Rule", "ADM-01")]
    public async Task ADM01_address_without_port_has_no_port()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, token) = await app.AddBotAsync();

        await Bot(app, token, "203.0.113.7").UpdatesAsync();

        Assert.Equal([$"Bot {botId} 203.0.113.7"], await app.AccessLogAsync());
    }

    [Fact]
    [Trait("Rule", "ADM-01")]
    public async Task ADM01_ipv4_written_as_ipv6_is_stored_as_ipv4()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, token) = await app.AddBotAsync();

        await Bot(app, token, "[::ffff:203.0.113.7]:51000").UpdatesAsync();

        Assert.Equal([$"Bot {botId} 203.0.113.7 51000"], await app.AccessLogAsync());
    }

    [Fact]
    [Trait("Rule", "ADM-01")]
    public async Task ADM01_call_fails_when_the_row_cannot_be_written_and_the_next_call_writes_it()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, token) = await app.AddBotAsync();
        var bot = Bot(app, token, "203.0.113.7:51000");

        await PostgresServer.ExecuteAsync(app.ConnectionString, "ALTER TABLE admin.access_log RENAME TO access_log_gone");
        var failed = await bot.GetAsync("/v1/updates?timeout=0", TestContext.Current.CancellationToken);
        await PostgresServer.ExecuteAsync(app.ConnectionString, "ALTER TABLE admin.access_log_gone RENAME TO access_log");
        await bot.UpdatesAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Equal([$"Bot {botId} 203.0.113.7 51000"], await app.AccessLogAsync());
    }

    [Theory]
    [Trait("Rule", "ADM-02")]
    [InlineData(59, 1)]
    [InlineData(60, 2)]
    public async Task ADM02_same_address_is_written_again_only_after_the_interval(int minutesLater, int rows)
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (_, token) = await app.AddBotAsync();
        var bot = Bot(app, token, "203.0.113.7:51000");

        await bot.UpdatesAsync();
        await bot.UpdatesAsync();
        app.Time.Advance(TimeSpan.FromMinutes(minutesLater));
        await bot.UpdatesAsync();

        Assert.Equal(rows, (await app.AccessLogAsync()).Length);
    }

    [Fact]
    [Trait("Rule", "ADM-02")]
    public async Task ADM02_new_address_is_written_at_once()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, token) = await app.AddBotAsync();

        await Bot(app, token, "203.0.113.7:51000").UpdatesAsync();
        app.Time.Advance(TimeSpan.FromSeconds(1));
        await Bot(app, token, "203.0.113.8:51000").UpdatesAsync();

        Assert.Equal([$"Bot {botId} 203.0.113.7 51000", $"Bot {botId} 203.0.113.8 51000"], await app.AccessLogAsync());
    }

    [Fact]
    [Trait("Rule", "ADM-02")]
    public async Task ADM02_calls_that_arrive_together_write_one_row()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (browser, _) = await app.LoginAsync();
        DalanApp.From(browser, Proxy).DefaultRequestHeaders.Add("X-Forwarded-For", "198.51.100.9:52000");

        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => browser.JsonAsync("/api/bots")));

        Assert.Single(await app.AccessLogAsync());
    }

    [Fact]
    [Trait("Rule", "ADM-03")]
    public async Task ADM03_sweep_deletes_only_rows_past_the_retention_time()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, token) = await app.AddBotAsync();

        await Bot(app, token, "203.0.113.7:51000").UpdatesAsync();
        app.Time.Advance(TimeSpan.FromDays(2));
        await Bot(app, token, "203.0.113.8:51000").UpdatesAsync();
        app.Time.Advance(TimeSpan.FromDays(179));
        await app.Services.GetRequiredService<AccessLogService>().SweepOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal([$"Bot {botId} 203.0.113.8 51000"], await app.AccessLogAsync());
    }

    /// <summary>A bot whose calls reach the server through the front proxy, which reports this client address.</summary>
    private static HttpClient Bot(DalanApp app, string token, string forwardedFor) =>
        DalanApp.From(app.BotClient(token, forwardedFor), Proxy);
}
