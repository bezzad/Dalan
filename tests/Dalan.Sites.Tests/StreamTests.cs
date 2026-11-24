using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Dalan.Sites.Tunnel;
using Dalan.Testing;
using Microsoft.Extensions.DependencyInjection;
using static Dalan.Sites.Tests.SitesApp;

namespace Dalan.Sites.Tests;

/// <summary>Milestone 15: answers with no body, the line per address, streamed answers, session end and reserved names.</summary>
public class StreamTests(PostgresServer postgres)
{
    [Theory]
    [Trait("Rule", "SITE-08")]
    [InlineData("GET", 304)]
    [InlineData("GET", 204)]
    [InlineData("HEAD", 200)]
    public async Task SITE08_answer_that_may_have_no_body_goes_out_with_none(string method, int status)
    {
        await using var app = await StartAsync(postgres);
        var (_, _, key) = await app.BuilderAsync();
        var agent = app.Agent(key);
        var session = await agent.SessionAsync("levelwise");

        var poll = agent.PollAsync(session);
        var call = app.Visitor("levelwise").SendAsync(new HttpRequestMessage(new HttpMethod(method), "/app.css"), Cancel);
        var request = Assert.Single(await poll);
        await agent.AnswerAsync(session, request.GetProperty("id").GetInt64(), status, "the app sent a body anyway",
            new Dictionary<string, string[]> { ["ETag"] = ["\"v1\""] });
        var response = await call;

        Assert.Equal((HttpStatusCode)status, response.StatusCode);
        Assert.Equal("\"v1\"", response.Headers.ETag!.Tag);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(Cancel));
    }

    [Fact]
    [Trait("Rule", "SITE-08")]
    public async Task SITE08_sites_domain_with_a_port_routes_and_shows_the_port()
    {
        await using var app = await StartAsync(postgres, ("Sites:Domain", Domain + ":5000"), ("Sites:Scheme", "http"));
        var (_, _, key) = await app.BuilderAsync();
        var agent = app.Agent(key);
        var opened = await (await agent.OpenAsync("levelwise")).Content.ReadFromJsonAsync<JsonElement>(Cancel);
        var session = opened.GetProperty("session").GetString()!;

        var poll = agent.PollAsync(session);
        var call = app.Visitor("levelwise").GetAsync("/", Cancel);
        var request = Assert.Single(await poll);
        await agent.AnswerAsync(session, request.GetProperty("id").GetInt64(), 200, "home");

        Assert.Equal($"http://levelwise.{Domain}:5000", opened.GetProperty("sites")[0].GetProperty("url").GetString());
        Assert.Equal("home", await (await call).Content.ReadAsStringAsync(Cancel));
    }

    [Fact]
    [Trait("Rule", "SITE-10")]
    public async Task SITE10_stream_that_started_in_time_may_run_longer()
    {
        await using var app = await StartAsync(postgres);
        var (_, _, key) = await app.BuilderAsync();
        var agent = app.Agent(key);
        var session = await agent.SessionAsync("levelwise");

        var poll = agent.PollAsync(session);
        var call = app.Visitor("levelwise").GetAsync("/events", HttpCompletionOption.ResponseHeadersRead, Cancel);
        var id = Assert.Single(await poll).GetProperty("id").GetInt64();
        await agent.PartAsync(session, id, 200, "a", more: true);
        var response = await call;
        app.Time.Advance(TimeSpan.FromSeconds(31));
        var late = await agent.PartAsync(session, id, 0, "b", more: false);

        Assert.Equal(HttpStatusCode.NoContent, late.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ab", await response.Content.ReadAsStringAsync(Cancel));
    }

    [Fact]
    [Trait("Rule", "SITE-11")]
    public async Task SITE11_stream_over_the_answer_limit_is_stopped()
    {
        await using var app = await StartAsync(postgres, ("Sites:MaxAnswerMb", "1"));
        var (_, _, key) = await app.BuilderAsync();
        var agent = app.Agent(key);
        var session = await agent.SessionAsync("levelwise");
        var half = new string('x', 512 * 1024);

        var poll = agent.PollAsync(session);
        var call = app.Visitor("levelwise").GetAsync("/endless", HttpCompletionOption.ResponseHeadersRead, Cancel);
        var id = Assert.Single(await poll).GetProperty("id").GetInt64();
        await agent.PartAsync(session, id, 200, half, more: true);
        var response = await call;
        var reading = response.Content.ReadAsStringAsync(Cancel);
        var second = await agent.PartAsync(session, id, 0, half, more: true);
        var over = await agent.PartAsync(session, id, 0, "y", more: true);
        var after = await agent.PartAsync(session, id, 0, "z", more: true);

        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(HttpStatusCode.Gone, over.StatusCode);
        Assert.Equal(HttpStatusCode.Gone, after.StatusCode);
        Assert.Equal("request_gone", await after.ErrorCodeAsync());
        await Assert.ThrowsAnyAsync<Exception>(() => reading);
    }

    [Fact]
    [Trait("Rule", "SITE-12")]
    public async Task SITE12_burst_waits_in_line_and_only_a_full_line_gets_429()
    {
        await using var app = await StartAsync(postgres, ("Sites:MaxPendingPerAddress", "2"), ("Sites:MaxQueuedPerAddress", "1"));
        var (_, _, key) = await app.BuilderAsync();
        var agent = app.Agent(key);
        var session = await agent.SessionAsync("levelwise");
        var busy = app.Visitor("levelwise", "5.6.7.8:1000");

        var first = busy.GetAsync("/1", Cancel);
        var second = busy.GetAsync("/2", Cancel);
        var atAgent = new List<JsonElement>();
        while (atAgent.Count < 2) atAgent.AddRange(await agent.PollAsync(session));
        var third = busy.GetAsync("/3", Cancel);
        await Task.Delay(200, Cancel);
        var fourth = await busy.GetAsync("/4", Cancel);
        var other = app.Visitor("levelwise", "9.9.9.9:1000").GetAsync("/5", Cancel);
        var otherRequest = Assert.Single(await agent.PollAsync(session));
        await agent.AnswerAsync(session, otherRequest.GetProperty("id").GetInt64(), 200, "other");
        Assert.False(third.IsCompleted);
        await agent.AnswerAsync(session, atAgent[0].GetProperty("id").GetInt64(), 200, "1");
        var thirdRequest = Assert.Single(await agent.PollAsync(session));
        foreach (var request in new[] { atAgent[1], thirdRequest })
            await agent.AnswerAsync(session, request.GetProperty("id").GetInt64(), 200, "ok");

        Assert.Equal(HttpStatusCode.TooManyRequests, fourth.StatusCode);
        Assert.Equal("/3", thirdRequest.GetProperty("path").GetString());
        Assert.Equal(HttpStatusCode.OK, (await other).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await first).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await second).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await third).StatusCode);
    }

    [Fact]
    [Trait("Rule", "SITE-12")]
    public async Task SITE12_request_that_waits_in_line_too_long_gets_429()
    {
        await using var app = await StartAsync(postgres, ("Sites:MaxPendingPerAddress", "1"));
        var (_, _, key) = await app.BuilderAsync();
        var agent = app.Agent(key);
        var session = await agent.SessionAsync("levelwise");
        var busy = app.Visitor("levelwise", "5.6.7.8:1000");

        // The first holds the one place as a stream, which has no timeout of its own.
        var first = busy.GetAsync("/1", HttpCompletionOption.ResponseHeadersRead, Cancel);
        var atAgent = Assert.Single(await agent.PollAsync(session));
        await agent.PartAsync(session, atAgent.GetProperty("id").GetInt64(), 200, "a", more: true);
        await first;
        var waiting = busy.GetAsync("/2", Cancel);
        for (var step = 0; step < 50 && !waiting.IsCompleted; step++)
        {
            app.Time.Advance(TimeSpan.FromSeconds(30));
            await Task.WhenAny(waiting, Task.Delay(100, Cancel));
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await waiting).StatusCode);
        await agent.PartAsync(session, atAgent.GetProperty("id").GetInt64(), 0, "b", more: false);
        Assert.Equal("ab", await (await first).Content.ReadAsStringAsync(Cancel));
    }

    [Fact]
    [Trait("Rule", "SITE-22")]
    public async Task SITE22_event_stream_reaches_the_visitor_part_by_part_and_each_part_gets_an_ack()
    {
        await using var app = await StartAsync(postgres);
        var (_, _, key) = await app.BuilderAsync();
        var session = await app.Agent(key).SessionAsync("levelwise");
        using var socket = await ConnectAsync(app, key, session);

        var call = app.Visitor("levelwise").GetAsync("/events", HttpCompletionOption.ResponseHeadersRead, Cancel);
        var id = (await ReceiveAsync(socket)).GetProperty("id").GetInt64();
        await SendAsync(socket, new { id, status = 200, headers = new Dictionary<string, string[]> { ["Content-Type"] = ["text/event-stream"] },
            body = Base64("data: 1\n\n"), more = true });
        var response = await call;
        var events = await response.Content.ReadAsStreamAsync(Cancel);
        var first = await events.ReadSomeAsync();
        await SendAsync(socket, new { id, status = 0, body = Base64("data: 2\n\n"), more = true });
        var second = await events.ReadSomeAsync();
        var ack = await ReceiveAsync(socket);

        Assert.Equal("data: 1\n\n", first);
        Assert.Equal("data: 2\n\n", second);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(id, ack.GetProperty("id").GetInt64());
        Assert.True(ack.GetProperty("ack").GetBoolean());
    }

    [Fact]
    [Trait("Rule", "SITE-22")]
    public async Task SITE22_visitor_who_leaves_a_stream_makes_the_server_send_cancel()
    {
        await using var app = await StartAsync(postgres);
        var (_, _, key) = await app.BuilderAsync();
        var session = await app.Agent(key).SessionAsync("levelwise");
        using var socket = await ConnectAsync(app, key, session);
        using var leave = CancellationTokenSource.CreateLinkedTokenSource(Cancel);

        var call = app.Visitor("levelwise").GetAsync("/events", HttpCompletionOption.ResponseHeadersRead, leave.Token);
        var id = (await ReceiveAsync(socket)).GetProperty("id").GetInt64();
        await SendAsync(socket, new { id, status = 200, body = Base64("data: 1\n\n"), more = true });
        var response = await call;
        await leave.CancelAsync();
        response.Dispose();
        var cancel = await ReceiveAsync(socket, message => message.TryGetProperty("cancel", out var value) && value.ValueKind == JsonValueKind.True);

        Assert.Equal(id, cancel.GetProperty("id").GetInt64());
    }

    [Fact]
    [Trait("Rule", "SITE-22")]
    public async Task SITE22_stream_by_long_polling_arrives_in_order_and_a_late_part_gets_410()
    {
        await using var app = await StartAsync(postgres);
        var (_, _, key) = await app.BuilderAsync();
        var agent = app.Agent(key);
        var session = await agent.SessionAsync("levelwise");

        var poll = agent.PollAsync(session);
        var call = app.Visitor("levelwise").GetAsync("/file", HttpCompletionOption.ResponseHeadersRead, Cancel);
        var id = Assert.Single(await poll).GetProperty("id").GetInt64();
        await agent.PartAsync(session, id, 200, "a", more: true);
        var response = await call;
        var reading = response.Content.ReadAsStringAsync(Cancel);
        await agent.PartAsync(session, id, 0, "b", more: true);
        await agent.PartAsync(session, id, 0, "c", more: false);
        var late = await agent.PartAsync(session, id, 0, "d", more: true);

        Assert.Equal("abc", await reading);
        Assert.Equal(HttpStatusCode.Gone, late.StatusCode);
    }

    [Fact]
    [Trait("Rule", "SITE-22")]
    public async Task SITE22_session_gives_the_agent_the_part_size_and_window()
    {
        await using var app = await StartAsync(postgres, ("Sites:StreamChunkKb", "32"), ("Sites:StreamWindow", "4"));
        var (_, _, key) = await app.BuilderAsync();

        var opened = await (await app.Agent(key).OpenAsync("levelwise")).Content.ReadFromJsonAsync<JsonElement>(Cancel);

        Assert.Equal(32, opened.GetProperty("stream_chunk_kb").GetInt32());
        Assert.Equal(4, opened.GetProperty("stream_window").GetInt32());
    }

    [Fact]
    [Trait("Rule", "SITE-23")]
    public async Task SITE23_ended_session_is_offline_at_once()
    {
        await using var app = await StartAsync(postgres);
        var (browser, _, key) = await app.BuilderAsync();
        var agent = app.Agent(key);
        var session = await agent.SessionAsync("levelwise");
        var poll = agent.PollAsync(session);
        var waiting = app.Visitor("levelwise").GetAsync("/", Cancel);
        Assert.Single(await poll);

        var ended = await agent.DeleteAsync($"/api/tunnel/sessions/{session}", Cancel);
        var next = await app.Visitor("levelwise").GetAsync("/", Cancel);
        var again = await agent.DeleteAsync($"/api/tunnel/sessions/{session}", Cancel);

        Assert.Equal(HttpStatusCode.NoContent, ended.StatusCode);
        Assert.Equal(HttpStatusCode.BadGateway, (await waiting).StatusCode);
        Assert.Equal(HttpStatusCode.BadGateway, next.StatusCode);
        Assert.Equal("site_offline", next.SiteError());
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        await browser.WaitOnlineAsync("levelwise", online: false);
    }

    [Fact]
    [Trait("Rule", "SITE-23")]
    public async Task SITE23_closed_websocket_ends_its_session()
    {
        await using var app = await StartAsync(postgres);
        var (_, _, key) = await app.BuilderAsync();
        var session = await app.Agent(key).SessionAsync("levelwise");
        var socket = await ConnectAsync(app, key, session);
        var hub = app.Services.GetRequiredService<TunnelHub>();

        socket.Abort(); // The agent's connection is lost.
        socket.Dispose();
        for (var i = 0; i < 200 && hub.Find("levelwise") is not null; i++) await Task.Delay(10, Cancel);
        var response = await app.Visitor("levelwise").GetAsync("/", Cancel);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("site_offline", response.SiteError());
    }

    [Fact]
    [Trait("Rule", "SITE-24")]
    public async Task SITE24_builder_reserves_a_name_with_no_agent()
    {
        await using var app = await StartAsync(postgres);
        var (browser, _, _) = await app.BuilderAsync("A");
        var (other, _, _) = await app.BuilderAsync("B");

        var made = await browser.PostAsJsonAsync("/api/sites", new { name = "LevelWise" }, Cancel);
        var again = await browser.PostAsJsonAsync("/api/sites", new { name = "levelwise" }, Cancel);
        var taken = await other.PostAsJsonAsync("/api/sites", new { name = "levelwise" }, Cancel);
        var bad = await browser.PostAsJsonAsync("/api/sites", new { name = "api" }, Cancel);
        var list = await browser.GetFromJsonAsync<JsonElement>("/api/sites", Cancel);
        var site = await made.Content.ReadFromJsonAsync<JsonElement>(Cancel);

        Assert.Equal(HttpStatusCode.Created, made.StatusCode);
        Assert.Equal("levelwise", site.GetProperty("name").GetString());
        Assert.Equal($"https://levelwise.{Domain}", site.GetProperty("url").GetString());
        Assert.False(site.GetProperty("online").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal("name_taken", await taken.ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal("bad_name", await bad.ErrorCodeAsync());
        Assert.Equal("levelwise", Assert.Single(list.GetProperty("sites").EnumerateArray()).GetProperty("name").GetString());
    }

    private static string Base64(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

    private static async Task<WebSocket> ConnectAsync(DalanApp app, string key, string session)
    {
        var client = app.Server.CreateWebSocketClient();
        client.ConfigureRequest = request => request.Headers.Authorization = "Bearer " + key;
        return await client.ConnectAsync(new Uri($"ws://localhost/api/tunnel/sessions/{session}/ws"), Cancel);
    }

    private static Task SendAsync(WebSocket socket, object message) =>
        socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(message), WebSocketMessageType.Text, true, Cancel);

    /// <summary>The next message, or the next one that matches.</summary>
    private static async Task<JsonElement> ReceiveAsync(WebSocket socket, Func<JsonElement, bool>? wanted = null)
    {
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var received = await socket.ReceiveAsync(buffer, Cancel);
            var message = JsonSerializer.Deserialize<JsonElement>(buffer.AsSpan(0, received.Count));
            if (wanted is null || wanted(message)) return message;
        }
    }
}
