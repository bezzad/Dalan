using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Testing;

namespace Dalan.Relay.Tests;

/// <summary>Edit, delete, pin and the versions that let a client sync them (REL-16, REL-23 to REL-25, LIVE-02).</summary>
public class ChangeTests(PostgresServer postgres)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static async Task<string?> ErrorOfAsync(Task<HttpResponseMessage> call, HttpStatusCode status)
    {
        var response = await call;
        Assert.Equal(status, response.StatusCode);
        return await response.ErrorCodeAsync();
    }

    [Fact]
    [Trait("Rule", "REL-16")]
    [Trait("Rule", "REL-23")]
    public async Task REL16_sync_after_a_version_brings_an_edit_of_an_old_message()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var wait = await chat.BotSaysAsync("wait");
        await chat.Browser.SayAsync(chat.BotId, "ok");

        Assert.Equal(HttpStatusCode.OK, (await chat.EditAsync(wait, new { text = "done" })).StatusCode);

        var changed = Assert.Single(await chat.Browser.HistoryAsync(chat.BotId, "after=3"));
        Assert.Equal(wait, changed.GetProperty("id").GetInt64());
        Assert.Equal(4, changed.GetProperty("version").GetInt64());
        Assert.Equal("done", changed.GetProperty("text").GetString());
    }

    [Fact]
    [Trait("Rule", "REL-23")]
    public async Task REL23_edit_sets_text_version_and_edited_time()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var id = await chat.BotSaysAsync("wait");
        app.Time.Advance(TimeSpan.FromMinutes(1));

        var response = await chat.EditAsync(id, new { text = "done" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var returned = await response.Content.ReadFromJsonAsync<JsonElement>(Cancel);
        Assert.Equal("done", returned.GetProperty("text").GetString());
        var message = await chat.MessageAsync(id);
        Assert.Equal("done", message.GetProperty("text").GetString());
        Assert.Equal(3, message.GetProperty("version").GetInt64());
        Assert.Equal(app.Time.GetUtcNow(), message.GetProperty("edited").GetDateTimeOffset());
    }

    [Fact]
    [Trait("Rule", "REL-23")]
    public async Task REL23_edit_of_the_text_alone_removes_the_inline_buttons()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var id = await chat.BotSaysAsync("pick", new[] { new[] { new { text = "A", callback_data = "a" } } });

        await chat.EditAsync(id, new { text = "picked" });

        Assert.False((await chat.MessageAsync(id)).TryGetProperty("inline_keyboard", out _));
    }

    [Fact]
    [Trait("Rule", "REL-23")]
    public async Task REL23_edit_of_the_buttons_alone_keeps_the_text()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var id = await chat.BotSaysAsync("pick");

        await chat.EditAsync(id, new { inline_keyboard = new[] { new[] { new { text = "B", callback_data = "b" } } } });

        var message = await chat.MessageAsync(id);
        Assert.Equal("pick", message.GetProperty("text").GetString());
        Assert.Equal("b", message.GetProperty("inline_keyboard")[0][0].GetProperty("callback_data").GetString());
    }

    [Fact]
    [Trait("Rule", "REL-23")]
    public async Task REL23_edits_that_are_not_allowed_are_refused()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var id = await chat.BotSaysAsync("same");

        Assert.Equal("not_editable", await ErrorOfAsync(chat.EditAsync(1, new { text = "x" }), HttpStatusCode.BadRequest));
        Assert.Equal("message_not_found", await ErrorOfAsync(chat.EditAsync(99, new { text = "x" }), HttpStatusCode.NotFound));
        Assert.Equal("not_modified", await ErrorOfAsync(chat.EditAsync(id, new { text = "same" }), HttpStatusCode.BadRequest));
        Assert.Equal("text_empty", await ErrorOfAsync(chat.EditAsync(id, new { text = "" }), HttpStatusCode.BadRequest));
        Assert.Equal("same", (await chat.MessageAsync(id)).GetProperty("text").GetString());
    }

    [Fact]
    [Trait("Rule", "REL-23")]
    public async Task REL23_text_of_a_file_message_cannot_be_edited()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var response = await chat.BotUploadAsync([1, 2, 3], "a.pdf", "document", "caption");
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>(Cancel)).GetProperty("id").GetInt64();

        Assert.Equal("no_text", await ErrorOfAsync(chat.EditAsync(id, new { text = "x" }), HttpStatusCode.BadRequest));
    }

    [Fact]
    [Trait("Rule", "REL-24")]
    public async Task REL24_delete_leaves_a_stub_that_a_sync_brings()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var id = await chat.BotSaysAsync("secret");

        var response = await chat.DeleteAsync(id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(await response.Content.ReadFromJsonAsync<bool>(Cancel));
        var stub = Assert.Single(await chat.Browser.HistoryAsync(chat.BotId, "after=2"));
        Assert.True(stub.GetProperty("deleted").GetBoolean());
        Assert.Equal("", stub.GetProperty("text").GetString());
    }

    [Fact]
    [Trait("Rule", "REL-24")]
    public async Task REL24_second_delete_is_not_found()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        await chat.DeleteAsync(1);

        Assert.Equal("message_not_found", await ErrorOfAsync(chat.DeleteAsync(1), HttpStatusCode.NotFound));
    }

    [Fact]
    [Trait("Rule", "REL-24")]
    public async Task REL24_deleted_pinned_message_is_no_longer_pinned()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        await chat.PinAsync(1);

        await chat.DeleteAsync(1);

        Assert.Equal(JsonValueKind.Null, (await chat.PageAsync()).GetProperty("pinned").ValueKind);
    }

    [Fact]
    [Trait("Rule", "REL-25")]
    public async Task REL25_pin_shows_in_every_page()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var id = await chat.BotSaysAsync("rules");
        await chat.BotSaysAsync("later");

        Assert.Equal(HttpStatusCode.OK, (await chat.PinAsync(id)).StatusCode);

        var page = await chat.PageAsync("limit=1");
        Assert.Equal(id, page.GetProperty("pinned").GetProperty("id").GetInt64());
        Assert.True(page.GetProperty("pinned").GetProperty("pinned").GetBoolean());
    }

    [Fact]
    [Trait("Rule", "REL-25")]
    public async Task REL25_pinning_another_message_unpins_the_first()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var second = await chat.BotSaysAsync("two");
        await chat.PinAsync(1);

        await chat.PinAsync(second);

        var page = await chat.PageAsync();
        Assert.Equal(second, page.GetProperty("pinned").GetProperty("id").GetInt64());
        Assert.False((await chat.MessageAsync(1)).TryGetProperty("pinned", out _));
        // Both changes reach a syncing client.
        Assert.Equal([1, second], (await chat.Browser.HistoryAsync(chat.BotId, "after=2")).Ids().Order());
    }

    [Fact]
    [Trait("Rule", "REL-25")]
    public async Task REL25_unpin_clears_the_pin_and_missing_message_is_not_found()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        await chat.PinAsync(1);

        Assert.Equal(HttpStatusCode.OK, (await chat.UnpinAsync()).StatusCode);

        Assert.Equal(JsonValueKind.Null, (await chat.PageAsync()).GetProperty("pinned").ValueKind);
        Assert.Equal("message_not_found", await ErrorOfAsync(chat.PinAsync(99), HttpStatusCode.NotFound));
    }

    [Fact]
    [Trait("Rule", "REL-25")]
    public async Task REL25_bot_cannot_change_a_chat_it_does_not_have()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var (_, otherToken) = await app.AddBotAsync("other");
        var other = chat with { Bot = app.BotClient(otherToken) };

        Assert.Equal("chat_not_active", await ErrorOfAsync(other.PinAsync(1), HttpStatusCode.Forbidden));
        Assert.Equal("chat_not_active", await ErrorOfAsync(other.DeleteAsync(1), HttpStatusCode.Forbidden));
    }

    [Fact]
    [Trait("Rule", "LIVE-02")]
    public async Task LIVE02_edit_reaches_the_stream_with_a_higher_version()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var chat = await Chat.StartAsync(app);
        var id = await chat.BotSaysAsync("wait");
        using var stream = await EventStreamReader.OpenAsync(chat.Browser);

        await chat.EditAsync(id, new { text = "done" });

        var message = await stream.NextMessageAsync("done");
        Assert.Equal(id, message.GetProperty("id").GetInt64());
        Assert.Equal(3, message.GetProperty("version").GetInt64());
    }
}
