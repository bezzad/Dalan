using Dalan.Relay.Chats;
using Dalan.Relay.Tests;
using Dalan.Testing;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace Dalan.Telegram.Tests;

/// <summary>TG-12: the real Telegram.Bot client, pointed at the test server. A wrong shape fails in a real parser.</summary>
public class TelegramLibraryTests(PostgresServer postgres)
{
    [Fact]
    [Trait("Rule", "TG-12")]
    public async Task TG12_telegram_bot_for_dotnet_works_with_only_the_address_and_token_changed()
    {
        var cancel = TestContext.Current.CancellationToken;
        var (app, _, botId, token) = await TelegramBot.StartAsync(postgres);
        await using var _1 = app;
        var user = await app.AddUserAsync("Sara", "sara");
        await app.UserSaysAsync(botId, user, "/start");

        var client = new TelegramBotClient(new TelegramBotClientOptions(token, baseUrl: "http://localhost"), app.CreateClient());

        var me = await client.GetMe(cancel);
        Assert.Equal(("testbot", true), (me.Username, me.IsBot));

        var update = Assert.Single(await client.GetUpdates(timeout: 0, cancellationToken: cancel));
        Assert.Equal("/start", update.Message!.Text);
        Assert.Equal(user, update.Message.From!.Id);
        Assert.Equal(user, update.Message.Chat.Id);
        Assert.Equal(global::Telegram.Bot.Types.Enums.MessageEntityType.BotCommand, Assert.Single(update.Message.Entities!).Type);

        var sent = await client.SendMessage(update.Message.Chat.Id, "سلام",
            replyMarkup: new ReplyKeyboardMarkup([["Yes", "No"], ["Cancel"]]) { ResizeKeyboard = true }, cancellationToken: cancel);
        Assert.Equal("سلام", sent.Text);

        Assert.Empty(await client.GetUpdates(offset: update.Id + 1, timeout: 0, cancellationToken: cancel));
        var stored = await app.MessagesAsync();
        Assert.Equal(DeliveryState.Delivered, stored[0].Delivery);
        Assert.Equal([["Yes", "No"], ["Cancel"]], stored[^1].Content.ReplyKeyboard!.ToRows());
    }
}
