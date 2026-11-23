using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Testing;

namespace Dalan.Relay.Tests;

/// <summary>A started chat: one logged-in person and one bot, with the bot's and the person's HTTP clients.</summary>
internal sealed record Chat(DalanApp App, HttpClient Bot, long BotId, HttpClient Browser, long UserId)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    /// <summary>The person sent "/start", so the chat has message 1.</summary>
    public static async Task<Chat> StartAsync(DalanApp app)
    {
        var (botId, token) = await app.AddBotAsync();
        var (browser, userId) = await app.LoginAsync();
        Assert.Equal(HttpStatusCode.OK, (await browser.SayAsync(botId, "/start")).StatusCode);
        return new Chat(app, app.BotClient(token), botId, browser, userId);
    }

    /// <summary>The bot sends a message (any JSON fields besides user_id) and returns its id.</summary>
    public async Task<long> BotSaysAsync(string text, object? inlineKeyboard = null)
    {
        var response = await Bot.PostAsJsonAsync("/v1/messages", new { user_id = UserId, text, inline_keyboard = inlineKeyboard }, Cancel);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Cancel)).GetProperty("id").GetInt64();
    }

    public Task<HttpResponseMessage> EditAsync(long messageId, object body) =>
        Bot.PatchAsJsonAsync($"/v1/messages/{UserId}/{messageId}", body, Cancel);

    public Task<HttpResponseMessage> DeleteAsync(long messageId) => Bot.DeleteAsync($"/v1/messages/{UserId}/{messageId}", Cancel);

    public Task<HttpResponseMessage> PinAsync(long messageId) =>
        Bot.PutAsJsonAsync($"/v1/chats/{UserId}/pin", new { message_id = messageId }, Cancel);

    public Task<HttpResponseMessage> UnpinAsync() => Bot.DeleteAsync($"/v1/chats/{UserId}/pin", Cancel);

    public Task<HttpResponseMessage> PressAsync(long messageId, string data) =>
        Browser.PostAsJsonAsync($"/api/bots/{BotId}/callbacks", new { message_id = messageId, data }, Cancel);

    /// <summary>The user's whole answer for a page of the chat: "messages" and "pinned".</summary>
    public Task<JsonElement> PageAsync(string query = "") => Browser.JsonAsync($"/api/bots/{BotId}/messages?{query}");

    public async Task<JsonElement> MessageAsync(long id) =>
        (await PageAsync()).GetProperty("messages").EnumerateArray().Single(m => m.GetProperty("id").GetInt64() == id);

    /// <summary>A user upload (REL-28): the raw body with its type and the name and caption headers.</summary>
    public async Task<HttpResponseMessage> UploadAsync(byte[] bytes, string type, string? name, string? caption = null, string? voiceDuration = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/bots/{BotId}/files") { Content = new ByteArrayContent(bytes) };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(type);
        if (name is not null) request.Headers.Add("X-File-Name", Uri.EscapeDataString(name));
        if (caption is not null) request.Headers.Add("X-Caption", Uri.EscapeDataString(caption));
        if (voiceDuration is not null) request.Headers.Add("X-Voice-Duration", voiceDuration);
        return await Browser.SendAsync(request, Cancel);
    }

    /// <summary>A bot upload (REL-27) as multipart/form-data.</summary>
    public async Task<HttpResponseMessage> BotUploadAsync(byte[] bytes, string name, string kind, string text = "", string? type = null, int? duration = null)
    {
        using var form = new MultipartFormDataContent
        {
            { new StringContent(UserId.ToString(System.Globalization.CultureInfo.InvariantCulture)), "user_id" },
            { new StringContent(kind), "kind" },
            { new StringContent(text), "text" },
        };
        if (duration is not null) form.Add(new StringContent(duration.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)), "duration");
        var file = new ByteArrayContent(bytes);
        if (type is not null) file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(type);
        form.Add(file, "file", name);
        return await Bot.PostAsync("/v1/messages", form, Cancel);
    }
}
