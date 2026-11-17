using Dalan.Relay.Buttons;
using Dalan.Relay.Chats;
using Dalan.Relay.Tests;
using Dalan.Testing;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Types;
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

    [Fact]
    [Trait("Rule", "TG-12")]
    public async Task TG12_telegram_bot_for_dotnet_does_the_rich_calls()
    {
        var cancel = TestContext.Current.CancellationToken;
        var (app, _, botId, token) = await TelegramBot.StartAsync(postgres);
        await using var _1 = app;
        var user = await app.AddUserAsync("Sara", "sara");
        await app.UserSaysAsync(botId, user, "/start");
        var client = new TelegramBotClient(new TelegramBotClientOptions(token, baseUrl: "http://localhost"), app.CreateClient());
        var start = Assert.Single(await client.GetUpdates(timeout: 0, cancellationToken: cancel));

        await client.SetMyCommands([new global::Telegram.Bot.Types.BotCommand("help", "Help")], cancellationToken: cancel);
        Assert.Equal("help", Assert.Single(await client.GetMyCommands(cancellationToken: cancel)).Command);

        var pick = await client.SendMessage(user, "pick",
            replyMarkup: new InlineKeyboardMarkup(global::Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("Yes", "y")), cancellationToken: cancel);
        Assert.Equal("y", pick.ReplyMarkup!.InlineKeyboard.Single().Single().CallbackData);
        await app.Services.GetRequiredService<ButtonPresses>().PressAsync(botId, user, pick.Id, "y", cancel);
        var press = Assert.Single(await client.GetUpdates(offset: start.Id + 1, timeout: 0, cancellationToken: cancel)).CallbackQuery!;
        Assert.Equal(("y", pick.Id, user), (press.Data, press.Message!.Id, press.From.Id));
        await client.AnswerCallbackQuery(press.Id, "Saved", cancellationToken: cancel);

        var edited = await client.EditMessageText(user, pick.Id, "picked", cancellationToken: cancel);
        Assert.Equal("picked", edited.Text);
        Assert.NotNull(edited.EditDate);
        await client.PinChatMessage(user, pick.Id, cancellationToken: cancel);

        await using var picture = new MemoryStream([1, 2, 3]);
        var photo = await client.SendPhoto(user, InputFile.FromStream(picture, "cat.jpg"), caption: "cat", cancellationToken: cancel);
        Assert.Equal("cat", photo.Caption);
        var file = await client.GetFile(Assert.Single(photo.Photo!).FileId, cancel);
        await using var downloaded = new MemoryStream();
        await client.DownloadFile(file.FilePath!, downloaded, cancel);
        Assert.Equal([1, 2, 3], downloaded.ToArray());

        await client.DeleteMessage(user, photo.Id, cancel);
        var stored = await app.MessagesAsync();
        Assert.True(stored.Single(m => m.Seq == pick.Id).Pinned);
        Assert.True(stored.Single(m => m.Seq == photo.Id).Deleted);
    }
}
