using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Testing;

namespace Dalan.Relay.Tests;

/// <summary>Inline buttons, presses, answers and commands (REL-20 to REL-22, REL-26, LIVE-09).</summary>
public class ButtonTests(PostgresServer postgres)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static readonly object[][] YesAndSite =
        [[new { text = "Yes", callback_data = "y" }, new { text = "Site", url = "https://dalanet.ir" }]];

    [Fact]
    [Trait("Rule", "REL-20")]
    public async Task REL20_inline_buttons_are_stored_and_shown()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);

        var id = await chat.BotSaysAsync("pick", YesAndSite);

        var row = (await chat.MessageAsync(id)).GetProperty("inline_keyboard")[0];
        Assert.Equal("y", row[0].GetProperty("callback_data").GetString());
        Assert.False(row[0].TryGetProperty("url", out _));
        Assert.Equal("https://dalanet.ir", row[1].GetProperty("url").GetString());
    }

    [Theory]
    [Trait("Rule", "REL-20")]
    [InlineData("""{"text":"A","callback_data":"a","url":"https://dalanet.ir"}""")]
    [InlineData("""{"text":"A"}""")]
    [InlineData("""{"text":"A","callback_data":"12345678901"}""")]
    [InlineData("""{"text":"A","url":"javascript:alert(1)"}""")]
    [InlineData("""{"text":"","callback_data":"a"}""")]
    public async Task REL20_broken_button_is_400_and_nothing_is_stored(string button)
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Relay:MaxCallbackDataBytes", "10"));
        var chat = await Chat.StartAsync(app);
        var keyboard = JsonSerializer.Deserialize<JsonElement>($"[[{button}]]");

        var response = await chat.Bot.PostAsJsonAsync("/v1/messages", new { user_id = chat.UserId, text = "x", inline_keyboard = keyboard }, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_keyboard", await response.ErrorCodeAsync());
        Assert.Single(await app.MessagesAsync());
    }

    [Fact]
    [Trait("Rule", "REL-21")]
    public async Task REL21_press_reaches_the_bot_and_is_not_in_history()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var id = await chat.BotSaysAsync("pick", YesAndSite);
        var first = await chat.Bot.UpdatesAsync();
        await chat.Bot.UpdatesAsync($"timeout=0&offset={first[^1].Id() + 1}");

        var response = await chat.PressAsync(id, "y");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pressId = (await response.Content.ReadFromJsonAsync<JsonElement>(Cancel)).GetProperty("id").GetString();
        var update = Assert.Single(await chat.Bot.UpdatesAsync());
        Assert.False(update.TryGetProperty("message", out _));
        var callback = update.GetProperty("callback");
        Assert.Equal(pressId, callback.GetProperty("id").GetString());
        Assert.Equal("y", callback.GetProperty("data").GetString());
        Assert.Equal(id, callback.GetProperty("message").GetProperty("id").GetInt64());
        Assert.Equal(chat.UserId, callback.GetProperty("from").GetProperty("id").GetInt64());
        Assert.True(update.Id() > first[^1].Id());
        Assert.Equal(2, (await chat.Browser.HistoryAsync(chat.BotId)).Length);
    }

    [Theory]
    [Trait("Rule", "REL-21")]
    [InlineData(2, "nope")]
    [InlineData(1, "y")]
    [InlineData(9, "y")]
    public async Task REL21_invented_press_is_404_and_the_bot_gets_nothing(long messageId, string data)
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        await chat.BotSaysAsync("pick", YesAndSite);
        var all = await chat.Bot.UpdatesAsync();
        await chat.Bot.UpdatesAsync($"timeout=0&offset={all[^1].Id() + 1}");

        var response = await chat.PressAsync(messageId, data);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("button_not_found", await response.ErrorCodeAsync());
        Assert.Empty(await chat.Bot.UpdatesAsync());
    }

    private static async Task<(Chat Chat, string PressId)> PressedAsync(DalanApp app)
    {
        var chat = await Chat.StartAsync(app);
        var id = await chat.BotSaysAsync("pick", YesAndSite);
        var press = await chat.PressAsync(id, "y");
        return (chat, (await press.Content.ReadFromJsonAsync<JsonElement>(Cancel)).GetProperty("id").GetString()!);
    }

    [Fact]
    [Trait("Rule", "REL-22")]
    [Trait("Rule", "LIVE-09")]
    public async Task REL22_answer_reaches_the_stream_of_the_user_who_pressed()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (chat, pressId) = await PressedAsync(app);
        using var stream = await EventStreamReader.OpenAsync(chat.Browser);

        var response = await chat.Bot.PostAsJsonAsync($"/v1/callbacks/{pressId}/answer", new { text = "Saved", show_alert = true }, Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Heartbeats, and the "not delivered" mark of the message the press replaced (REL-31), come first.
        var block = await stream.NextAsync();
        while (!block!.StartsWith("event: answer", StringComparison.Ordinal)) block = await stream.NextAsync();
        var lines = block.Split('\n');
        var data = JsonSerializer.Deserialize<JsonElement>(lines[1]["data: ".Length..]);
        Assert.Equal(chat.BotId, data.GetProperty("bot_id").GetInt64());
        Assert.Equal(pressId, data.GetProperty("callback_id").GetString());
        Assert.Equal("Saved", data.GetProperty("text").GetString());
        Assert.True(data.GetProperty("show_alert").GetBoolean());
    }

    [Fact]
    [Trait("Rule", "REL-22")]
    public async Task REL22_unknown_old_or_foreign_press_is_refused()
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Relay:CallbackAnswerSeconds", "120"));
        var (chat, pressId) = await PressedAsync(app);
        var (_, otherToken) = await app.AddBotAsync("other");

        var unknown = await chat.Bot.PostAsync("/v1/callbacks/999/answer", null, Cancel);
        var foreign = await app.BotClient(otherToken).PostAsync($"/v1/callbacks/{pressId}/answer", null, Cancel);
        app.Time.Advance(TimeSpan.FromSeconds(121));
        var old = await chat.Bot.PostAsync($"/v1/callbacks/{pressId}/answer", null, Cancel);

        foreach (var response in new[] { unknown, foreign, old })
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("callback_not_found", await response.ErrorCodeAsync());
        }
    }

    [Fact]
    [Trait("Rule", "REL-26")]
    public async Task REL26_bot_and_user_read_the_same_commands()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var commands = new[] { new { command = "start", description = "Start" }, new { command = "help", description = "راهنما" } };

        var set = await chat.Bot.PutAsJsonAsync("/v1/commands", new { commands }, Cancel);

        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        foreach (var list in new[] { await chat.Bot.JsonAsync("/v1/commands"), await chat.Browser.JsonAsync($"/api/bots/{chat.BotId}/commands") })
        {
            var read = list.GetProperty("commands");
            Assert.Equal(["start", "help"], read.EnumerateArray().Select(c => c.GetProperty("command").GetString()));
            Assert.Equal("راهنما", read[1].GetProperty("description").GetString());
        }
    }

    [Theory]
    [Trait("Rule", "REL-26")]
    [InlineData("Start!", "Start")]
    [InlineData("start", "")]
    [InlineData("", "Start")]
    public async Task REL26_bad_command_is_400_and_changes_nothing(string command, string description)
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        await chat.Bot.PutAsJsonAsync("/v1/commands", new { commands = new[] { new { command = "old", description = "Old" } } }, Cancel);

        var response = await chat.Bot.PutAsJsonAsync("/v1/commands", new { commands = new[] { new { command, description } } }, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_commands", await response.ErrorCodeAsync());
        Assert.Equal("old", (await chat.Bot.JsonAsync("/v1/commands")).GetProperty("commands")[0].GetProperty("command").GetString());
    }

    [Fact]
    [Trait("Rule", "REL-26")]
    public async Task REL26_too_many_commands_is_400()
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Relay:MaxCommands", "1"));
        var chat = await Chat.StartAsync(app);
        var commands = new[] { new { command = "a", description = "A" }, new { command = "b", description = "B" } };

        var response = await chat.Bot.PutAsJsonAsync("/v1/commands", new { commands }, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
