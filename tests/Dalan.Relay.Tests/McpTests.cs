using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dalan.Testing;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Dalan.Relay.Tests;

/// <summary>REL-46 and REL-47: the MCP door, called with the official MCP client, like an agent would.</summary>
public class McpTests(PostgresServer postgres)
{
    private const string AdminPhone = "09129990001";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private Task<DalanApp> StartAsync(params (string Key, string Value)[] settings) =>
        RelayApp.StartAsync(postgres, [.. settings, ("Identity:AdminPhones", AdminPhone)]);

    /// <summary>A builder's access key. <paramref name="approve"/> false: the user only asked to be a builder.</summary>
    private static async Task<(string Key, long UserId)> KeyAsync(DalanApp app, bool approve = true)
    {
        var (admin, _) = await app.LoginAsync("Owner", AdminPhone);
        var (browser, userId) = await app.LoginAsync("Builder");
        if (approve)
            (await admin.PostAsJsonAsync($"/api/admin/users/{userId}/builder", new { approve = true }, Cancel)).EnsureSuccessStatusCode();
        var made = await (await browser.PostAsJsonAsync("/api/account/keys", new { name = "agent" }, Cancel))
            .Content.ReadFromJsonAsync<JsonElement>(Cancel);
        return (made.GetProperty("key").GetString()!, userId);
    }

    private static HttpClient WithKey(DalanApp app, string? key, string? endPoint = null)
    {
        var client = app.CreateClient();
        if (key is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return endPoint is null ? client : DalanApp.From(client, endPoint);
    }

    private static Task<McpClient> ConnectAsync(DalanApp app, string key, string? protocolVersion = null, string? endPoint = null)
    {
        var http = WithKey(app, key, endPoint);
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = new Uri(http.BaseAddress!, "/api/mcp"), TransportMode = HttpTransportMode.StreamableHttp },
            http, ownsHttpClient: true);
        return McpClient.CreateAsync(transport, new McpClientOptions { ProtocolVersion = protocolVersion }, cancellationToken: Cancel);
    }

    private static string Text(CallToolResult result) => Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;

    private static Task<CallToolResult> CallAsync(McpClient client, string tool, object? arguments = null) =>
        client.CallToolAsync(tool,
            arguments is null ? null : JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(arguments)),
            cancellationToken: Cancel).AsTask();

    [Theory]
    [Trait("Rule", "REL-46")]
    [InlineData(null)]
    [InlineData("2025-11-25")]
    public async Task REL46_lists_the_four_tools(string? protocolVersion)
    {
        await using var app = await StartAsync();
        var (key, _) = await KeyAsync(app);
        await using var client = await ConnectAsync(app, key, protocolVersion);

        var tools = await client.ListToolsAsync(cancellationToken: Cancel);

        Assert.Equal(["bot_status", "create_bot", "list_bots", "rotate_token"], tools.Select(t => t.Name).Order());
    }

    [Fact]
    [Trait("Rule", "REL-46")]
    public async Task REL46_create_bot_gives_a_working_token()
    {
        await using var app = await StartAsync();
        var (key, userId) = await KeyAsync(app);
        await using var client = await ConnectAsync(app, key);

        var result = await CallAsync(client, "create_bot", new { username = "weather_bot", name = "Weather" });
        var bot = JsonElement.Parse(Text(result));
        var list = JsonElement.Parse(Text(await CallAsync(client, "list_bots")));

        Assert.NotEqual(true, result.IsError);
        Assert.Equal("weather_bot", bot.GetProperty("username").GetString());
        Assert.Empty(await app.BotClient(bot.GetProperty("token").GetString()).UpdatesAsync());
        Assert.Equal("weather_bot", Assert.Single(list.GetProperty("bots").EnumerateArray()).GetProperty("username").GetString());
        Assert.Equal(1, await PostgresServer.ScalarAsync<long>(app.ConnectionString,
            $"SELECT count(*) FROM admin.audit_log WHERE action = 'bot.create' AND user_id = {userId}"));
    }

    [Fact]
    [Trait("Rule", "REL-46")]
    public async Task REL46_status_and_rotate_token()
    {
        await using var app = await StartAsync();
        var (key, _) = await KeyAsync(app);
        await using var client = await ConnectAsync(app, key);
        var bot = JsonElement.Parse(Text(await CallAsync(client, "create_bot", new { username = "weather_bot", name = "Weather" })));
        var id = bot.GetProperty("id").GetInt64();
        Assert.Empty(await app.BotClient(bot.GetProperty("token").GetString()).UpdatesAsync());

        var status = JsonElement.Parse(Text(await CallAsync(client, "bot_status", new { bot_id = id })));
        var rotated = JsonElement.Parse(Text(await CallAsync(client, "rotate_token", new { bot_id = id })));

        Assert.True(status.GetProperty("online").GetBoolean());
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await app.BotClient(bot.GetProperty("token").GetString()).GetAsync("/v1/updates?timeout=0", Cancel)).StatusCode);
        Assert.Empty(await app.BotClient(rotated.GetProperty("token").GetString()).UpdatesAsync());
    }

    [Fact]
    [Trait("Rule", "REL-46")]
    public async Task REL46_refused_call_is_a_tool_error_with_the_message()
    {
        await using var app = await StartAsync();
        var (key, _) = await KeyAsync(app);
        await using var client = await ConnectAsync(app, key);
        await CallAsync(client, "create_bot", new { username = "weather_bot", name = "Weather" });

        var taken = await CallAsync(client, "create_bot", new { username = "weather_bot", name = "Weather" });
        var unknown = await CallAsync(client, "bot_status", new { bot_id = 999999 });

        Assert.True(taken.IsError);
        Assert.Equal("Another bot has this username. Choose another one.", Text(taken));
        Assert.True(unknown.IsError);
        Assert.Equal("You have no bot with this id.", Text(unknown));
    }

    /// <summary>A raw MCP call, as an agent sends it on the wire.</summary>
    private static Task<HttpResponseMessage> PostRawAsync(HttpClient client)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/mcp")
        {
            Content = new StringContent(
                """{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{"_meta":{"io.modelcontextprotocol/protocolVersion":"2026-07-28","io.modelcontextprotocol/clientCapabilities":{}}}}""",
                Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.Add("Mcp-Method", "tools/list");
        request.Headers.Add("MCP-Protocol-Version", "2026-07-28");
        return client.SendAsync(request, Cancel);
    }

    [Fact]
    [Trait("Rule", "REL-47")]
    public async Task REL47_no_key_gets_401()
    {
        await using var app = await StartAsync();

        var response = await PostRawAsync(WithKey(app, null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Rule", "REL-47")]
    public async Task REL47_not_a_builder_gets_403()
    {
        await using var app = await StartAsync();
        var (key, _) = await KeyAsync(app, approve: false);

        var response = await PostRawAsync(WithKey(app, key));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("not_builder", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "REL-47")]
    public async Task REL47_call_writes_the_access_log()
    {
        await using var app = await StartAsync();
        var (key, userId) = await KeyAsync(app);

        var response = await PostRawAsync(WithKey(app, key, "203.0.113.7:40001"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"User {userId} 203.0.113.7 40001", await app.AccessLogAsync());
    }

    [Fact]
    [Trait("Rule", "REL-47")]
    public async Task REL47_counts_the_per_user_limit()
    {
        await using var app = await StartAsync(("Relay:RateLimitWindowSeconds", "3600"), ("Relay:UserRequestLimit", "1"));
        var (key, _) = await KeyAsync(app);
        var client = WithKey(app, key);
        (await PostRawAsync(client)).EnsureSuccessStatusCode();

        var response = await PostRawAsync(client);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }
}
