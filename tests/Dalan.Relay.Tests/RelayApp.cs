using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Relay.Bots;
using Dalan.Relay.Chats;
using Dalan.Relay.Data;
using Dalan.Relay.Queue;
using Dalan.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Relay.Tests;

/// <summary>Short helpers for Relay tests on top of <see cref="DalanApp"/>.</summary>
internal static class RelayApp
{
    /// <summary>A server whose queue tells the test when a poll starts to wait.</summary>
    public static DalanApp Create(string database, params (string Key, string Value)[] settings) => new(
        database,
        settings.ToDictionary(s => s.Key, s => s.Value),
        web => web.ConfigureServices(services =>
        {
            services.AddSingleton<WatchedQueue>();
            services.AddSingleton<IUpdateQueue>(provider => provider.GetRequiredService<WatchedQueue>());
        }));

    public static async Task<DalanApp> StartAsync(PostgresServer postgres, params (string Key, string Value)[] settings)
    {
        var app = Create(await postgres.NewDatabaseAsync(), settings);
        await app.ReadyAsync();
        return app;
    }

    public static async Task<(long BotId, string Token)> AddBotAsync(this DalanApp app, string username = "testbot")
    {
        var (bot, token) = await app.Services.GetRequiredService<BotRegistry>()
            .CreateAsync(ownerUserId: 1, username, "Test bot", TestContext.Current.CancellationToken);
        return (bot.Id, token);
    }

    /// <summary>An HTTP client that calls the Bot API as this bot. <paramref name="ip"/> is what the front proxy would report.</summary>
    public static HttpClient BotClient(this DalanApp app, string? token, string? ip = null)
    {
        var client = app.CreateClient();
        if (token is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (ip is not null) client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
        return client;
    }

    /// <summary>A user sends a text to a bot, the way the Client API will in milestone 3.</summary>
    public static async Task<Message> UserSaysAsync(this DalanApp app, long botId, long userId, string text)
    {
        var (error, message) = await app.Services.GetRequiredService<ChatService>()
            .ReceiveFromUserAsync(botId, userId, text, null, TestContext.Current.CancellationToken);
        Assert.Equal(ChatError.None, error);
        return message!;
    }

    public static WatchedQueue Queue(this DalanApp app) => app.Services.GetRequiredService<WatchedQueue>();

    public static async Task<List<Message>> MessagesAsync(this DalanApp app)
    {
        await using var db = await app.Services.GetRequiredService<IDbContextFactory<RelayDb>>().CreateDbContextAsync();
        return await db.Messages.OrderBy(m => m.Id).ToListAsync();
    }

    public static async Task<DeliveryState?> DeliveryOfAsync(this DalanApp app, Message message) =>
        (await app.MessagesAsync()).Single(m => m.Id == message.Id).Delivery;

    /// <summary>Calls get-updates and returns the "updates" array.</summary>
    public static async Task<JsonElement[]> UpdatesAsync(this HttpClient bot, string query = "timeout=0")
    {
        var response = await bot.GetAsync($"/v1/updates?{query}", TestContext.Current.CancellationToken);
        return await response.UpdatesAsync();
    }

    public static async Task<JsonElement[]> UpdatesAsync(this HttpResponseMessage response)
    {
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return [.. body.GetProperty("updates").EnumerateArray()];
    }

    public static async Task<string?> ErrorCodeAsync(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("message").GetString()), "REL-11: an error needs a message");
        return body.GetProperty("error").GetString();
    }

    public static Task<HttpResponseMessage> SendAsync(this HttpClient bot, long userId, string text) =>
        bot.PostAsJsonAsync("/v1/messages", new { user_id = userId, text }, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> SendAsync(this HttpClient bot, long userId, string text, string[][] replyKeyboard) =>
        bot.PostAsJsonAsync("/v1/messages", new { user_id = userId, text, reply_keyboard = replyKeyboard }, TestContext.Current.CancellationToken);

    /// <summary>A person writes to a bot through the Client API.</summary>
    public static Task<HttpResponseMessage> SayAsync(this HttpClient browser, long botId, string text) =>
        browser.PostAsJsonAsync($"/api/bots/{botId}/messages", new { text }, TestContext.Current.CancellationToken);

    public static async Task<JsonElement> JsonAsync(this HttpClient client, string path)
    {
        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    /// <summary>The "messages" array of a chat, as the Client API returns it.</summary>
    public static async Task<JsonElement[]> HistoryAsync(this HttpClient browser, long botId, string query = "") =>
        [.. (await browser.JsonAsync($"/api/bots/{botId}/messages?{query}")).GetProperty("messages").EnumerateArray()];

    public static long[] Ids(this IEnumerable<JsonElement> messages) => [.. messages.Select(m => m.GetProperty("id").GetInt64())];

    public static long Id(this JsonElement update) => update.GetProperty("update_id").GetInt64();

    public static string? Text(this JsonElement update) => update.GetProperty("message").GetProperty("text").GetString();
}

/// <summary>The real queue, plus a signal each time a poll finds nothing and starts to wait.</summary>
internal sealed class WatchedQueue(IServiceProvider services) : IUpdateQueue, IDisposable
{
    private readonly InMemoryUpdateQueue _inner = ActivatorUtilities.CreateInstance<InMemoryUpdateQueue>(services);
    private readonly SemaphoreSlim _waits = new(0);

    /// <summary>Completes when a get-updates call found the queue empty and is now waiting.</summary>
    public async Task PollWaitsAsync() =>
        Assert.True(await _waits.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken), "no poll started to wait");

    public Batch Take(long botId, long offset, int limit)
    {
        var batch = _inner.Take(botId, offset, limit);
        if (batch.Updates.Count == 0) _waits.Release();
        return batch;
    }

    public IReadOnlyList<long> Enqueue(long botId, QueuedUpdate update) => _inner.Enqueue(botId, update);

    public IReadOnlyList<long> RemoveExpired() => _inner.RemoveExpired();

    public CancellationToken BeginPoll(long botId) => _inner.BeginPoll(botId);

    public void Dispose() => _waits.Dispose();
}
