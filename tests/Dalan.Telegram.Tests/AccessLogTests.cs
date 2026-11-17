using Dalan.Relay.Tests;
using Dalan.Testing;

namespace Dalan.Telegram.Tests;

public class AccessLogTests(PostgresServer postgres)
{
    /// <summary>The front proxy on this machine, as the server sees its connection.</summary>
    private const string Proxy = "127.0.0.1:40000";

    private static TelegramBot Through(DalanApp app, string token, string endPoint, string? forwardedFor)
    {
        var client = DalanApp.From(app.CreateClient(), endPoint);
        if (forwardedFor is not null) client.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        return new TelegramBot(client, token);
    }

    [Fact]
    [Trait("Rule", "TG-13")]
    public async Task TG13_adapter_call_is_recorded_with_address_and_port()
    {
        var (app, _, botId, token) = await TelegramBot.StartAsync(postgres);
        await using var _1 = app;

        (await Through(app, token, Proxy, "203.0.113.7:51000").GetAsync("getMe")).IsOk();

        Assert.Equal([$"Bot {botId} 203.0.113.7 51000"], await app.AccessLogAsync());
    }

    [Fact]
    [Trait("Rule", "TG-13")]
    public async Task TG13_header_from_a_client_that_is_not_a_proxy_is_not_believed()
    {
        var (app, _, botId, token) = await TelegramBot.StartAsync(postgres);
        await using var _1 = app;

        (await Through(app, token, "198.51.100.20:53000", "9.9.9.9").GetAsync("getMe")).IsOk();

        Assert.Equal([$"Bot {botId} 198.51.100.20 53000"], await app.AccessLogAsync());
    }

    [Fact]
    [Trait("Rule", "TG-13")]
    public async Task TG13_wrong_token_writes_no_row()
    {
        var (app, _, botId, _) = await TelegramBot.StartAsync(postgres);
        await using var _1 = app;

        (await Through(app, $"{botId}:wrong", Proxy, "203.0.113.7:51000").GetAsync("getMe")).IsError(401, "Unauthorized");

        Assert.Empty(await app.AccessLogAsync());
    }
}
