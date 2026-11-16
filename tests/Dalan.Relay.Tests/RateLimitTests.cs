using System.Net;
using Dalan.Testing;

namespace Dalan.Relay.Tests;

/// <summary>The window is one hour here, so "the same window" does not depend on how fast the test runs.</summary>
public class RateLimitTests(PostgresServer postgres)
{
    private static readonly (string, string) LongWindow = ("Relay:RateLimitWindowSeconds", "3600");

    private static async Task<HttpStatusCode> PollAsync(HttpClient client) =>
        (await client.GetAsync("/v1/updates?timeout=0", TestContext.Current.CancellationToken)).StatusCode;

    [Fact]
    [Trait("Rule", "SEC-01")]
    public async Task SEC01_call_over_the_bot_limit_is_429_with_retry_after()
    {
        await using var app = await RelayApp.StartAsync(postgres, LongWindow, ("Relay:BotRequestLimit", "2"));
        var (_, token) = await app.AddBotAsync();
        var bot = app.BotClient(token);

        Assert.Equal(HttpStatusCode.OK, await PollAsync(bot));
        Assert.Equal(HttpStatusCode.OK, await PollAsync(bot));
        var third = await bot.GetAsync("/v1/updates?timeout=0", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Equal("rate_limited", await third.ErrorCodeAsync());
        Assert.True(third.Headers.RetryAfter?.Delta > TimeSpan.Zero, "Retry-After is missing");
    }

    [Fact]
    [Trait("Rule", "SEC-01")]
    public async Task SEC01_one_bot_over_its_limit_does_not_block_another()
    {
        await using var app = await RelayApp.StartAsync(postgres, LongWindow, ("Relay:BotRequestLimit", "1"));
        var (_, busyToken) = await app.AddBotAsync("busy");
        var (_, otherToken) = await app.AddBotAsync("other");
        var busy = app.BotClient(busyToken);
        await PollAsync(busy);
        Assert.Equal(HttpStatusCode.TooManyRequests, await PollAsync(busy));

        Assert.Equal(HttpStatusCode.OK, await PollAsync(app.BotClient(otherToken)));
    }

    [Fact]
    [Trait("Rule", "SEC-01")]
    public async Task SEC01_wrong_tokens_do_not_use_up_the_bot_limit()
    {
        await using var app = await RelayApp.StartAsync(postgres, LongWindow, ("Relay:BotRequestLimit", "1"));
        var (botId, token) = await app.AddBotAsync();
        var attacker = app.BotClient($"{botId}:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, await PollAsync(attacker));

        Assert.Equal(HttpStatusCode.OK, await PollAsync(app.BotClient(token)));
    }

    [Fact]
    [Trait("Rule", "SEC-02")]
    public async Task SEC02_call_over_the_ip_limit_is_429_even_without_a_token()
    {
        await using var app = await RelayApp.StartAsync(postgres, LongWindow, ("Relay:IpRequestLimit", "2"));
        var client = app.BotClient(token: null, ip: "203.0.113.7");

        Assert.Equal(HttpStatusCode.Unauthorized, await PollAsync(client));
        Assert.Equal(HttpStatusCode.Unauthorized, await PollAsync(client));
        var third = await client.GetAsync("/v1/updates?timeout=0", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Equal("rate_limited", await third.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "SEC-02")]
    public async Task SEC02_addresses_behind_the_proxy_are_limited_one_by_one()
    {
        await using var app = await RelayApp.StartAsync(postgres, LongWindow, ("Relay:IpRequestLimit", "1"));
        var (_, token) = await app.AddBotAsync();
        var busy = app.BotClient(token, ip: "203.0.113.7");
        await PollAsync(busy);
        Assert.Equal(HttpStatusCode.TooManyRequests, await PollAsync(busy));

        Assert.Equal(HttpStatusCode.OK, await PollAsync(app.BotClient(token, ip: "203.0.113.8")));
    }
}
