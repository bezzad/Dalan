using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dalan.Relay.Tests;
using Dalan.Testing;

namespace Dalan.Telegram.Tests;

/// <summary>One answer of the adapter: the HTTP response and its JSON body.</summary>
internal sealed record Answer(HttpResponseMessage Http, JsonElement Body)
{
    public HttpStatusCode Status => Http.StatusCode;
    public JsonElement Result => Body.GetProperty("result");
    public string Description => Body.GetProperty("description").GetString()!;

    /// <summary>TG-02: the shape of an error.</summary>
    public void IsError(int code, string descriptionStart)
    {
        Assert.Equal(code, (int)Status);
        Assert.False(Body.GetProperty("ok").GetBoolean());
        Assert.Equal(code, Body.GetProperty("error_code").GetInt32());
        Assert.StartsWith(descriptionStart, Description, StringComparison.Ordinal);
    }

    public void IsOk()
    {
        Assert.True(Status == HttpStatusCode.OK, $"{(int)Status}: {Body}");
        Assert.True(Body.GetProperty("ok").GetBoolean());
    }

    /// <summary>Compares the result with a saved example of the pinned Bot API version, field by field.</summary>
    public void ResultEquals(string exampleFile)
    {
        IsOk();
        var expected = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Examples", exampleFile)));
        var actual = JsonNode.Parse(Result.GetRawText());
        Assert.True(JsonNode.DeepEquals(expected, actual), $"Expected {expected}\nActual {actual}");
    }
}

/// <summary>A bot that talks to Dalan the way a Telegram library does.</summary>
internal sealed class TelegramBot(HttpClient http, string token)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public static async Task<(DalanApp App, TelegramBot Bot, long BotId, string Token)> StartAsync(
        PostgresServer postgres, params (string Key, string Value)[] settings)
    {
        var app = await RelayApp.StartAsync(postgres, settings);
        var (botId, token) = await app.AddBotAsync();
        return (app, new TelegramBot(app.CreateClient(), token), botId, token);
    }

    public string Url(string method, string query = "") => $"/bot{token}/{method}?{query}";

    public async Task<Answer> GetAsync(string method, string query = "") =>
        await ReadAsync(await http.GetAsync(Url(method, query), Cancel));

    public async Task<Answer> PostJsonAsync(string method, object body) =>
        await ReadAsync(await http.PostAsJsonAsync(Url(method), body, Cancel));

    public async Task<Answer> PostAsync(string method, HttpContent content) =>
        await ReadAsync(await http.PostAsync(Url(method), content, Cancel));

    public Task<HttpResponseMessage> PollAsync(string query) => http.GetAsync(Url("getUpdates", query), Cancel);

    public static async Task<Answer> ReadAsync(HttpResponseMessage response) =>
        new(response, await response.Content.ReadFromJsonAsync<JsonElement>(Cancel));

    /// <summary>The list of updates of one getUpdates call.</summary>
    public async Task<JsonElement[]> UpdatesAsync(string query = "timeout=0")
    {
        var answer = await GetAsync("getUpdates", query);
        answer.IsOk();
        return [.. answer.Result.EnumerateArray()];
    }
}
