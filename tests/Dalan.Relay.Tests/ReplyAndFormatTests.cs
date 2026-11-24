using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Relay.Contracts;
using Dalan.Relay.Data;
using Dalan.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Relay.Tests;

/// <summary>Replies, formatting, switch buttons, caption edits, the press wait and one bot's manifest (REL-18, REL-20, REL-23, REL-52 to REL-54).</summary>
public class ReplyAndFormatTests(PostgresServer postgres)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    private static JsonElement Json(string text) => JsonSerializer.Deserialize<JsonElement>(text);

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response)
    {
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Cancel)}");
        return await response.Content.ReadFromJsonAsync<JsonElement>(Cancel);
    }

    private static Task<HttpResponseMessage> BotSendsAsync(Chat chat, object body) => chat.Bot.PostAsJsonAsync("/v1/messages", body, Cancel);

    [Fact]
    [Trait("Rule", "REL-18")]
    public async Task REL18_settings_have_the_press_wait()
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Relay:ClientPressWaitSeconds", "9"));
        var (browser, _) = await app.LoginAsync();

        Assert.Equal(9, (await browser.JsonAsync("/api/settings")).GetProperty("press_wait_seconds").GetInt32());
    }

    [Fact]
    [Trait("Rule", "REL-52")]
    public async Task REL52_user_replies_to_the_bot_and_the_bot_gets_a_copy()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        await chat.Bot.UpdatesAsync();
        var question = await chat.BotSaysAsync("Which session?");

        var sent = await BodyAsync(await chat.Browser.PostAsJsonAsync(
            $"/api/bots/{chat.BotId}/messages", new { text = "the second", reply_to = question }, Cancel));

        var reply = sent.GetProperty("reply_to");
        Assert.Equal(question, reply.GetProperty("id").GetInt64());
        Assert.Equal("bot", reply.GetProperty("from").GetString());
        Assert.Equal("Which session?", reply.GetProperty("text").GetString());
        var update = (await chat.Bot.UpdatesAsync())[^1].GetProperty("message");
        Assert.Equal("Which session?", update.GetProperty("reply_to").GetProperty("text").GetString());
        Assert.Equal(question, (await chat.MessageAsync(sent.GetProperty("id").GetInt64())).GetProperty("reply_to").GetProperty("id").GetInt64());
    }

    [Fact]
    [Trait("Rule", "REL-52")]
    public async Task REL52_a_file_replies_with_the_header()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var question = await chat.BotSaysAsync("Send a picture");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/bots/{chat.BotId}/files") { Content = new ByteArrayContent(Png) };
        request.Content.Headers.ContentType = new("image/png");
        request.Headers.Add("X-File-Name", "a.png");
        request.Headers.Add("X-Reply-To", question.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var sent = await BodyAsync(await chat.Browser.SendAsync(request, Cancel));

        Assert.Equal(question, sent.GetProperty("reply_to").GetProperty("id").GetInt64());
    }

    [Fact]
    [Trait("Rule", "REL-52")]
    public async Task REL52_bot_replies_to_the_user_and_the_copy_stays_after_an_edit()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var said = await BodyAsync(await chat.Browser.SayAsync(chat.BotId, "hello"));
        var userMessage = said.GetProperty("id").GetInt64();

        var sent = await BodyAsync(await BotSendsAsync(chat, new { user_id = chat.UserId, text = "hi", reply_to = userMessage }));

        Assert.Equal("user", sent.GetProperty("reply_to").GetProperty("from").GetString());
        await BodyAsync(await chat.EditAsync(sent.GetProperty("id").GetInt64(), new { text = "hi again" }));
        var shown = await chat.MessageAsync(sent.GetProperty("id").GetInt64());
        Assert.Equal("hi again", shown.GetProperty("text").GetString());
        Assert.Equal(userMessage, shown.GetProperty("reply_to").GetProperty("id").GetInt64());
        Assert.Equal("hello", shown.GetProperty("reply_to").GetProperty("text").GetString());
    }

    [Fact]
    [Trait("Rule", "REL-52")]
    public async Task REL52_reply_to_a_missing_or_deleted_message_is_404()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var gone = await chat.BotSaysAsync("old");
        await chat.DeleteAsync(gone);
        var before = (await app.MessagesAsync()).Count;

        var user = await chat.Browser.PostAsJsonAsync($"/api/bots/{chat.BotId}/messages", new { text = "x", reply_to = gone }, Cancel);
        var bot = await BotSendsAsync(chat, new { user_id = chat.UserId, text = "x", reply_to = 99 });

        Assert.Equal(HttpStatusCode.NotFound, user.StatusCode);
        Assert.Equal("message_not_found", await user.ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.NotFound, bot.StatusCode);
        Assert.Equal("message_not_found", await bot.ErrorCodeAsync());
        Assert.Equal(before, (await app.MessagesAsync()).Count);
    }

    [Fact]
    [Trait("Rule", "REL-53")]
    public async Task REL53_entities_are_stored_and_given_to_everyone()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var entities = Json("""[{"type":"bold","offset":3,"length":3},{"type":"text_link","offset":0,"length":2,"url":"https://dalanet.ir"}]""");

        var sent = await BodyAsync(await BotSendsAsync(chat, new { user_id = chat.UserId, text = "hi Ali", entities }));

        Assert.Equal(2, sent.GetProperty("entities").GetArrayLength());
        var shown = (await chat.MessageAsync(sent.GetProperty("id").GetInt64())).GetProperty("entities");
        Assert.Equal("text_link", shown[0].GetProperty("type").GetString());
        Assert.Equal("https://dalanet.ir", shown[0].GetProperty("url").GetString());
        Assert.Equal(("bold", 3, 3), (shown[1].GetProperty("type").GetString(), shown[1].GetProperty("offset").GetInt32(), shown[1].GetProperty("length").GetInt32()));
    }

    [Theory]
    [Trait("Rule", "REL-53")]
    [InlineData("""[{"type":"bold","offset":4,"length":3}]""")]
    [InlineData("""[{"type":"blink","offset":0,"length":1}]""")]
    [InlineData("""[{"type":"text_link","offset":0,"length":1}]""")]
    [InlineData("""[{"type":"text_link","offset":0,"length":1,"url":"javascript:alert(1)"}]""")]
    [InlineData("""[{"type":"bold","offset":0,"length":3},{"type":"italic","offset":2,"length":3}]""")]
    [InlineData("""[{"type":"bold","offset":0,"length":0}]""")]
    public async Task REL53_broken_entities_are_400_and_nothing_is_stored(string entities)
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var before = (await app.MessagesAsync()).Count;

        var response = await BotSendsAsync(chat, new { user_id = chat.UserId, text = "hi Ali", entities = Json(entities) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_entities", await response.ErrorCodeAsync());
        Assert.Equal(before, (await app.MessagesAsync()).Count);
    }

    [Fact]
    [Trait("Rule", "REL-53")]
    public async Task REL53_nested_entities_are_fine_and_too_many_are_not()
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Relay:MaxTextEntities", "2"));
        var chat = await Chat.StartAsync(app);

        var nested = await BotSendsAsync(chat, new
        {
            user_id = chat.UserId, text = "abc", entities = Json("""[{"type":"bold","offset":0,"length":3},{"type":"italic","offset":1,"length":1}]"""),
        });
        var tooMany = await BotSendsAsync(chat, new
        {
            user_id = chat.UserId, text = "abc",
            entities = Json("""[{"type":"bold","offset":0,"length":1},{"type":"bold","offset":1,"length":1},{"type":"bold","offset":2,"length":1}]"""),
        });

        Assert.Equal(HttpStatusCode.OK, nested.StatusCode);
        Assert.Equal("bad_entities", await tooMany.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "REL-23")]
    [Trait("Rule", "REL-53")]
    public async Task REL23_text_edit_replaces_entities_and_a_button_edit_keeps_them()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var sent = await BodyAsync(await BotSendsAsync(chat, new
        {
            user_id = chat.UserId, text = "wait", entities = Json("""[{"type":"italic","offset":0,"length":4}]"""),
        }));
        var id = sent.GetProperty("id").GetInt64();

        await BodyAsync(await chat.EditAsync(id, new { inline_keyboard = new[] { new[] { new { text = "B", callback_data = "b" } } } }));
        Assert.Equal("italic", (await chat.MessageAsync(id)).GetProperty("entities")[0].GetProperty("type").GetString());

        await BodyAsync(await chat.EditAsync(id, new { text = "done", entities = Json("""[{"type":"bold","offset":0,"length":4}]""") }));
        Assert.Equal("bold", (await chat.MessageAsync(id)).GetProperty("entities")[0].GetProperty("type").GetString());

        await BodyAsync(await chat.EditAsync(id, new { text = "done" }));
        Assert.False((await chat.MessageAsync(id)).TryGetProperty("entities", out _));
    }

    [Fact]
    [Trait("Rule", "REL-20")]
    public async Task REL20_switch_query_button_is_stored()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);

        var id = await chat.BotSaysAsync("todo", new[] { new[] { new { text = "Edit", switch_query = "todo 7 " } } });

        var button = (await chat.MessageAsync(id)).GetProperty("inline_keyboard")[0][0];
        Assert.Equal("todo 7 ", button.GetProperty("switch_query").GetString());
        Assert.False(button.TryGetProperty("callback_data", out _));
    }

    [Fact]
    [Trait("Rule", "REL-20")]
    public async Task REL20_a_button_with_two_kinds_is_bad()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);

        var response = await BotSendsAsync(chat, new
        {
            user_id = chat.UserId, text = "x", inline_keyboard = new[] { new[] { new { text = "A", callback_data = "a", switch_query = "" } } },
        });

        Assert.Equal("bad_keyboard", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "REL-54")]
    public async Task REL54_manifest_of_a_bot_installs_its_chat()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, _) = await app.AddBotAsync("mofidbot");
        Assert.Equal(BotError.None, await app.Services.GetRequiredService<IBotGateway>().SetIconAsync(botId, new MemoryStream(Png), Cancel));

        var response = await app.CreateClient().GetAsync("/@MofidBot/manifest.webmanifest", Cancel);

        var manifest = await BodyAsync(response);
        Assert.Equal("application/manifest+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("/@mofidbot", manifest.GetProperty("start_url").GetString());
        Assert.Equal("/@mofidbot", manifest.GetProperty("scope").GetString());
        Assert.Equal("/@mofidbot", manifest.GetProperty("id").GetString());
        Assert.Equal("Test bot", manifest.GetProperty("name").GetString());
        Assert.Equal("standalone", manifest.GetProperty("display").GetString());
        Assert.StartsWith($"/api/bots/{botId}/icon?v=", manifest.GetProperty("icons")[0].GetProperty("src").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "REL-54")]
    public async Task REL54_bot_page_links_its_manifest_and_other_pages_the_main_one()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var client = app.CreateClient();

        var botPage = await client.GetStringAsync("/@mofidbot", Cancel);
        var chats = await client.GetStringAsync("/chats", Cancel);

        Assert.Contains("""<link rel="manifest" href="/@mofidbot/manifest.webmanifest">""", botPage, StringComparison.Ordinal);
        Assert.Contains("""<link rel="manifest" href="/manifest.webmanifest">""", chats, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "REL-54")]
    public async Task REL54_no_manifest_for_an_unknown_or_suspended_bot()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, _) = await app.AddBotAsync("mofidbot");
        await using (var db = await app.Services.GetRequiredService<IDbContextFactory<RelayDb>>().CreateDbContextAsync(Cancel))
            await db.Bots.Where(b => b.Id == botId).ExecuteUpdateAsync(set => set.SetProperty(b => b.SuspendedAt, DateTimeOffset.UtcNow), Cancel);
        var client = app.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/@nobot/manifest.webmanifest", Cancel)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/@mofidbot/manifest.webmanifest", Cancel)).StatusCode);
    }
}
