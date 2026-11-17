using Dalan.Relay.Contracts;
using Dalan.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Telegram.Tests;

public class SuspendTests(PostgresServer postgres)
{
    [Fact]
    [Trait("Rule", "TG-22")]
    public async Task TG22_suspended_bot_gets_403()
    {
        var (app, bot, botId, _) = await TelegramBot.StartAsync(postgres);
        await using var _1 = app;
        await app.Services.GetRequiredService<IBotAdmin>().SetSuspendedAsync(botId, true, TestContext.Current.CancellationToken);

        var answer = await bot.GetAsync("getMe");

        answer.IsError(403, "Forbidden: bot was suspended");
    }
}
