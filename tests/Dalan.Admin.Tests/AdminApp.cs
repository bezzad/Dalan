using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Relay.Bots;
using Dalan.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Admin.Tests;

/// <summary>Short helpers for Admin tests on top of <see cref="DalanApp"/>.</summary>
internal static class AdminApp
{
    public const string AdminPhone = "09129990001";

    public static CancellationToken Cancel => TestContext.Current.CancellationToken;

    /// <summary>A server where <see cref="AdminPhone"/> is an admin (AUTH-18).</summary>
    public static async Task<DalanApp> StartAsync(PostgresServer postgres, params (string Key, string Value)[] settings)
    {
        var all = settings.Append(("Identity:AdminPhones", AdminPhone)).ToDictionary(s => s.Item1, s => s.Item2);
        var app = new DalanApp(await postgres.NewDatabaseAsync(), all);
        await app.ReadyAsync();
        return app;
    }

    /// <summary>The admin's browser and user id.</summary>
    public static Task<(HttpClient Browser, long UserId)> AdminAsync(this DalanApp app) => app.LoginAsync("Admin", AdminPhone);

    public static async Task<(long BotId, string Token)> AddBotAsync(this DalanApp app, string username = "testbot", long owner = 1)
    {
        var (bot, token) = await app.Services.GetRequiredService<BotRegistry>().CreateAsync(owner, username, "Test bot", Cancel);
        return (bot.Id, token);
    }

    /// <summary>A person starts the bot's chat (the membership row counts as one of the bot's users, ADM-12).</summary>
    public static async Task SayAsync(this HttpClient browser, long botId, string text) =>
        (await browser.PostAsJsonAsync($"/api/bots/{botId}/messages", new { text }, Cancel)).EnsureSuccessStatusCode();

    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync(Cancel)}");
        return await response.Content.ReadFromJsonAsync<JsonElement>(Cancel);
    }

    public static async Task<JsonElement> JsonAsync(this HttpClient client, string path) =>
        await (await client.GetAsync(path, Cancel)).JsonAsync();

    public static async Task<string?> ErrorCodeAsync(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancel);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("message").GetString()), "an error needs a message");
        return body.GetProperty("error").GetString();
    }

    public static async Task<JsonElement> SettingAsync(this HttpClient admin, string key) =>
        (await admin.JsonAsync("/api/admin/settings")).GetProperty("settings").EnumerateArray()
            .Single(s => s.GetProperty("key").GetString() == key);

    public static Task<HttpResponseMessage> SetAsync(this HttpClient admin, string key, string value) =>
        admin.PutAsJsonAsync($"/api/admin/settings/{key}", new { value }, Cancel);

    public static JsonElement[] Rows(this JsonElement body, string name = "rows") => [.. body.GetProperty(name).EnumerateArray()];
}
