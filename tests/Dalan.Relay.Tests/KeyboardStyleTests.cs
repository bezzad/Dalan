using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Relay.Data;
using Dalan.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Relay.Tests;

/// <summary>Button styles and keyboard options (REL-17, REL-20).</summary>
public class KeyboardStyleTests(PostgresServer postgres)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static readonly string[][] Yes = [["Yes"]];

    private static Task<HttpResponseMessage> SendAsync(Chat chat, object body) =>
        chat.Bot.PostAsJsonAsync("/v1/messages", body, Cancel);

    [Fact]
    [Trait("Rule", "REL-17")]
    public async Task REL17_rows_style_and_flags_are_kept()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        object[][] keyboard =
        [
            ["a1", "a2", "a3", "a4", "a5"],
            ["b1", "b2", "b3", "b4"],
            ["c1", "c2", new { text = "➕", style = "success" }],
        ];

        var response = await SendAsync(chat, new { user_id = chat.UserId, text = "menu", reply_keyboard = keyboard, reply_keyboard_resize = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var shown = (await chat.Browser.HistoryAsync(chat.BotId))[^1];
        Assert.Equal([5, 4, 3], shown.GetProperty("reply_keyboard").EnumerateArray().Select(row => row.GetArrayLength()));
        Assert.Equal("➕", shown.GetProperty("reply_keyboard")[2][2].GetString());
        var options = shown.GetProperty("reply_keyboard_options");
        Assert.True(options.GetProperty("resize").GetBoolean());
        Assert.False(options.GetProperty("persistent").GetBoolean());
        Assert.False(options.GetProperty("one_time").GetBoolean());
        var styles = options.GetProperty("styles");
        Assert.Equal("success", styles[2][2].GetString());
        Assert.Equal(JsonValueKind.Null, styles[2][1].ValueKind);
        Assert.Equal(JsonValueKind.Null, styles[0][0].ValueKind);
        Assert.Equal([5, 4, 3], styles.EnumerateArray().Select(row => row.GetArrayLength()));
        // The bot's own answer shows the same.
        Assert.Equal("success", (await response.Content.ReadFromJsonAsync<JsonElement>(Cancel))
            .GetProperty("reply_keyboard_options").GetProperty("styles")[2][2].GetString());
    }

    [Fact]
    [Trait("Rule", "REL-17")]
    public async Task REL17_persistent_and_one_time_flags_come_without_styles()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);

        (await SendAsync(chat, new
        {
            user_id = chat.UserId, text = "x", reply_keyboard = Yes,
            reply_keyboard_persistent = true, reply_keyboard_one_time = true,
        })).EnsureSuccessStatusCode();

        var options = (await chat.Browser.HistoryAsync(chat.BotId))[^1].GetProperty("reply_keyboard_options");
        Assert.True(options.GetProperty("persistent").GetBoolean());
        Assert.True(options.GetProperty("one_time").GetBoolean());
        Assert.False(options.GetProperty("resize").GetBoolean());
    }

    [Fact]
    [Trait("Rule", "REL-17")]
    public async Task REL17_a_keyboard_without_style_or_flag_has_no_options()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);

        (await SendAsync(chat, new { user_id = chat.UserId, text = "x", reply_keyboard = new object[][] { ["Yes", new { text = "No" }] } }))
            .EnsureSuccessStatusCode();

        var shown = (await chat.Browser.HistoryAsync(chat.BotId))[^1];
        Assert.Equal(["Yes", "No"], shown.GetProperty("reply_keyboard")[0].EnumerateArray().Select(b => b.GetString()));
        Assert.False(shown.TryGetProperty("reply_keyboard_options", out _));
    }

    [Theory]
    [Trait("Rule", "REL-17")]
    [InlineData("""[[{"text":"x","style":"pink"}]]""")]
    [InlineData("""[[{"text":"x","style":"SUCCESS"}]]""")]
    public async Task REL17_unknown_style_is_400_and_nothing_is_stored(string keyboard)
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);

        var response = await SendAsync(chat, new { user_id = chat.UserId, text = "x", reply_keyboard = JsonSerializer.Deserialize<JsonElement>(keyboard) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_keyboard", await response.ErrorCodeAsync());
        Assert.Single(await app.MessagesAsync());
    }

    [Fact]
    [Trait("Rule", "REL-17")]
    public async Task REL17_rows_saved_before_styles_existed_still_read()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        (await chat.Bot.SendAsync(chat.UserId, "old", [["Yes", "No"]])).EnsureSuccessStatusCode();
        await using (var db = await app.Services.GetRequiredService<IDbContextFactory<RelayDb>>().CreateDbContextAsync(Cancel))
        {
            // The old shape: no flags and no styles in the keyboard.
            await db.Database.ExecuteSqlRawAsync(
                """UPDATE relay.messages SET content = content #- '{{ReplyKeyboard,Persistent}}' #- '{{ReplyKeyboard,Resize}}' #- '{{ReplyKeyboard,OneTime}}' WHERE direction = 1""", Cancel);
        }

        var shown = (await chat.Browser.HistoryAsync(chat.BotId))[^1];

        Assert.Equal(["Yes", "No"], shown.GetProperty("reply_keyboard")[0].EnumerateArray().Select(b => b.GetString()));
        Assert.False(shown.TryGetProperty("reply_keyboard_options", out _));
    }

    [Fact]
    [Trait("Rule", "REL-20")]
    public async Task REL20_inline_button_style_is_kept()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);

        var id = await chat.BotSaysAsync("pick", new object[][]
        {
            [new { text = "Yes", callback_data = "y", style = "danger" }, new { text = "Site", url = "https://dalanet.ir" }],
        });

        var row = (await chat.MessageAsync(id)).GetProperty("inline_keyboard")[0];
        Assert.Equal("danger", row[0].GetProperty("style").GetString());
        Assert.False(row[1].TryGetProperty("style", out _));
    }

    [Fact]
    [Trait("Rule", "REL-20")]
    public async Task REL20_unknown_inline_style_is_400_and_nothing_is_stored()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);

        var response = await SendAsync(chat, new
        {
            user_id = chat.UserId, text = "x", inline_keyboard = new[] { new[] { new { text = "A", callback_data = "a", style = "pink" } } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_keyboard", await response.ErrorCodeAsync());
        Assert.Single(await app.MessagesAsync());
    }
}
