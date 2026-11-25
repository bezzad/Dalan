using Dalan.Identity.Contracts;
using Dalan.Relay.Tests;
using Dalan.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Telegram.Tests;

/// <summary>TG-26: a chat moved by an account merge, the way Telegram tells a bot that a group moved.</summary>
public class MovedChatTests(PostgresServer postgres)
{
    private static async Task<(DalanApp App, TelegramBot Bot, long From, long To)> MovedAsync(PostgresServer postgres)
    {
        var (app, bot, botId, _) = await TelegramBot.StartAsync(postgres);
        var from = await app.AddUserAsync("Sara");
        var to = await app.AddUserAsync("Sara phone");
        await app.UserSaysAsync(botId, from, "/start");
        await app.Services.GetServices<IUserMergeListener>().Single().UserMergedAsync(from, to, TestContext.Current.CancellationToken);
        return (app, bot, from, to);
    }

    [Fact]
    [Trait("Rule", "TG-26")]
    public async Task TG26_update_is_a_message_with_migrate_to_chat_id()
    {
        var (app, bot, from, to) = await MovedAsync(postgres);
        await using var _1 = app;

        var message = (await bot.UpdatesAsync())
            .Select(u => u.GetProperty("message"))
            .Single(m => m.TryGetProperty("migrate_to_chat_id", out _));

        Assert.Equal(from, message.GetProperty("chat").GetProperty("id").GetInt64());
        Assert.Equal(to, message.GetProperty("migrate_to_chat_id").GetInt64());
    }

    [Fact]
    [Trait("Rule", "TG-26")]
    public async Task TG26_send_to_the_old_chat_gets_migrate_to_chat_id()
    {
        var (app, bot, from, to) = await MovedAsync(postgres);
        await using var _1 = app;

        var answer = await bot.PostJsonAsync("sendMessage", new { chat_id = from, text = "hi" });

        answer.IsError(400, "Bad Request: group chat was upgraded to a supergroup chat");
        Assert.Equal(to, answer.Body.GetProperty("parameters").GetProperty("migrate_to_chat_id").GetInt64());
    }
}
