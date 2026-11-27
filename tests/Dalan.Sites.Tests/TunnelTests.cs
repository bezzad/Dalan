using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dalan.Testing;
using static Dalan.Sites.Tests.SitesApp;

namespace Dalan.Sites.Tests;

public class TunnelTests(PostgresServer postgres)
{
    [Fact]
    [Trait("Rule", "SITE-01")]
    public async Task SITE01_sites_table_has_a_unique_name()
    {
        await using var app = await StartAsync(postgres);

        var unique = await PostgresServer.ScalarAsync<bool>(app.ConnectionString,
            "SELECT indisunique FROM pg_index i JOIN pg_class c ON c.oid = i.indexrelid WHERE c.relname = 'ix_sites_name'");

        Assert.True(unique);
    }

    [Fact]
    [Trait("Rule", "SITE-02")]
    public async Task SITE02_name_in_capitals_is_stored_in_lower_case()
    {
        await using var app = await StartAsync(postgres);
        var (_, _, key) = await app.BuilderAsync();

        var response = await app.Agent(key).OpenAsync("MySite");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var site = Assert.Single(body.GetProperty("sites").EnumerateArray());
        Assert.Equal("mysite", site.GetProperty("name").GetString());
        Assert.Equal($"https://mysite.{Domain}", site.GetProperty("url").GetString());
    }

    [Theory]
    [Trait("Rule", "SITE-02")]
    [Trait("Rule", "SEC-16")]
    [InlineData("-x")]
    [InlineData("ab")]
    [InlineData("a_b")]
    [InlineData("api")]
    [InlineData("view")]
    [InlineData("end-")]
    public async Task SITE02_bad_or_reserved_name_is_refused(string name)
    {
        await using var app = await StartAsync(postgres);
        var (_, _, key) = await app.BuilderAsync();

        var response = await app.Agent(key).OpenAsync("good-one", name);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_name", await response.ErrorCodeAsync());
        Assert.Equal(0, await PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT count(*) FROM sites.sites"));
    }

    [Fact]
    [Trait("Rule", "SITE-03")]
    public async Task SITE03_user_who_is_not_a_builder_cannot_open_a_tunnel()
    {
        await using var app = await StartAsync(postgres);
        var (_, _, key) = await app.BuilderAsync(approve: false);

        var response = await app.Agent(key).OpenAsync("mysite");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("not_builder", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "SITE-03")]
    public async Task SITE03_name_of_another_builder_is_taken_and_nothing_is_made()
    {
        await using var app = await StartAsync(postgres);
        var (_, _, first) = await app.BuilderAsync("A");
        var (_, _, second) = await app.BuilderAsync("B");
        await app.Agent(first).SessionAsync("mysite");

        var response = await app.Agent(second).OpenAsync("shop", "mysite");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("name_taken", await response.ErrorCodeAsync());
        Assert.Equal(1, await PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT count(*) FROM sites.sites"));
    }

    [Fact]
    [Trait("Rule", "SITE-03")]
    public async Task SITE03_new_site_over_the_limit_is_refused()
    {
        await using var app = await StartAsync(postgres, ("Sites:MaxSitesPerBuilder", "2"));
        var (_, _, key) = await app.BuilderAsync();
        await app.Agent(key).SessionAsync("one", "two");

        var again = await app.Agent(key).OpenAsync("one", "two");
        var more = await app.Agent(key).OpenAsync("three");

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, more.StatusCode);
        Assert.Equal("site_limit", await more.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "SITE-04")]
    public async Task SITE04_newer_session_takes_the_site_and_the_older_one_ends()
    {
        await using var app = await StartAsync(postgres);
        var (_, _, key) = await app.BuilderAsync();
        var agent = app.Agent(key);
        var older = await agent.SessionAsync("mysite");
        var newer = await agent.SessionAsync("mysite");

        var oldPoll = await agent.GetAsync($"/api/tunnel/sessions/{older}/requests?timeout=0", Cancel);
        var visitor = app.Visitor("mysite").GetAsync("/", Cancel);
        var request = Assert.Single(await agent.PollAsync(newer));
        await agent.AnswerAsync(newer, request.GetProperty("id").GetInt64(), 200, "from the newer");

        Assert.Equal(HttpStatusCode.NotFound, oldPoll.StatusCode);
        Assert.Equal("session_ended", await oldPoll.ErrorCodeAsync());
        Assert.Equal("from the newer", await (await visitor).Content.ReadAsStringAsync(Cancel));
    }

    [Fact]
    [Trait("Rule", "SITE-04")]
    public async Task SITE04_tunnel_cannot_open_when_sites_are_off()
    {
        await using var app = await StartAsync(postgres, ("Sites:Domain", ""));
        var (_, _, key) = await app.BuilderAsync();

        var response = await app.Agent(key).OpenAsync("mysite");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("sites_off", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "SITE-05")]
    public async Task SITE05_request_and_answer_travel_over_the_websocket()
    {
        await using var app = await StartAsync(postgres);
        var (_, _, key) = await app.BuilderAsync();
        var session = await app.Agent(key).SessionAsync("mysite");
        var client = app.Server.CreateWebSocketClient();
        client.ConfigureRequest = request => request.Headers.Authorization = "Bearer " + key;
        using var socket = await client.ConnectAsync(new Uri($"ws://localhost/api/tunnel/sessions/{session}/ws"), Cancel);

        var visitor = app.Visitor("mysite").PostAsync("/hello?x=1", new StringContent("ping"), Cancel);
        var buffer = new byte[64 * 1024];
        var received = await socket.ReceiveAsync(buffer, Cancel);
        var request = JsonSerializer.Deserialize<JsonElement>(buffer.AsSpan(0, received.Count));
        var answer = JsonSerializer.SerializeToUtf8Bytes(new
        {
            id = request.GetProperty("id").GetInt64(),
            status = 201,
            headers = new Dictionary<string, string[]> { ["X-Home"] = ["yes"], ["Content-Type"] = ["text/plain"] },
            body = Convert.ToBase64String("pong"u8.ToArray()),
        });
        await socket.SendAsync(answer, System.Net.WebSockets.WebSocketMessageType.Text, true, Cancel);
        var response = await visitor;

        Assert.Equal("mysite", request.GetProperty("site").GetString());
        Assert.Equal("POST", request.GetProperty("method").GetString());
        Assert.Equal("/hello?x=1", request.GetProperty("path").GetString());
        Assert.Equal("ping", Encoding.UTF8.GetString(Convert.FromBase64String(request.GetProperty("body").GetString()!)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("yes", response.Headers.GetValues("X-Home").Single());
        Assert.Equal("pong", await response.Content.ReadAsStringAsync(Cancel));
    }

    [Fact]
    [Trait("Rule", "SITE-06")]
    public async Task SITE06_request_and_answer_travel_by_long_polling()
    {
        await using var app = await StartAsync(postgres);
        var (_, _, key) = await app.BuilderAsync();
        var agent = app.Agent(key);
        var session = await agent.SessionAsync("mysite");

        var poll = agent.PollAsync(session);
        var visitor = app.Visitor("mysite").GetAsync("/", Cancel);
        var request = Assert.Single(await poll);
        var posted = await agent.AnswerAsync(session, request.GetProperty("id").GetInt64(), 200, "hello");
        var late = await agent.AnswerAsync(session, request.GetProperty("id").GetInt64(), 200, "again");

        Assert.Equal(HttpStatusCode.NoContent, posted.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, late.StatusCode);
        Assert.Equal("hello", await (await visitor).Content.ReadAsStringAsync(Cancel));
    }

    [Fact]
    [Trait("Rule", "SITE-06")]
    public async Task SITE06_poll_with_nothing_to_do_returns_empty_after_its_timeout()
    {
        await using var app = await StartAsync(postgres);
        var (_, _, key) = await app.BuilderAsync();
        var agent = app.Agent(key);
        var session = await agent.SessionAsync("mysite");

        var poll = agent.PollAsync(session, timeout: 5);
        await Task.Delay(100, Cancel);
        Assert.False(poll.IsCompleted);
        // On a busy machine the server may start its timer after the first step of the clock, so step until the poll ends.
        for (var step = 0; step < 50 && !poll.IsCompleted; step++)
        {
            app.Time.Advance(TimeSpan.FromSeconds(5));
            await Task.WhenAny(poll, Task.Delay(100, Cancel));
        }

        Assert.Empty(await poll);
    }

    [Fact]
    [Trait("Rule", "SITE-07")]
    public async Task SITE07_session_with_no_poll_for_the_idle_time_is_offline()
    {
        await using var app = await StartAsync(postgres);
        var (_, _, key) = await app.BuilderAsync();
        var agent = app.Agent(key);
        var session = await agent.SessionAsync("mysite");
        await agent.PollAsync(session, timeout: 0);

        app.Time.Advance(TimeSpan.FromSeconds(61));
        var response = await app.Visitor("mysite").GetAsync("/", Cancel);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("site_offline", response.SiteError());
    }

    [Fact]
    [Trait("Rule", "SITE-08")]
    public async Task SITE08_visitor_forwarded_and_hop_by_hop_headers_are_replaced()
    {
        await using var app = await StartAsync(postgres);
        var (_, _, key) = await app.BuilderAsync();
        var agent = app.Agent(key);
        var session = await agent.SessionAsync("mysite");
        var visitor = app.Visitor("mysite", "5.6.7.8:1234");
        visitor.DefaultRequestHeaders.Add("X-Forwarded-For", "1.1.1.1");
        visitor.DefaultRequestHeaders.Add("Connection", "x-secret");
        visitor.DefaultRequestHeaders.Add("X-Secret", "only this hop");
        visitor.DefaultRequestHeaders.Add("Accept", "text/html");

        var poll = agent.PollAsync(session);
        var call = visitor.GetAsync("/", Cancel);
        var request = Assert.Single(await poll);
        var headers = request.GetProperty("headers");
        await agent.AnswerAsync(session, request.GetProperty("id").GetInt64(), 200);
        await call;

        string[] Values(string name) => headers.EnumerateObject()
            .Where(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            .SelectMany(p => p.Value.EnumerateArray().Select(v => v.GetString()!)).ToArray();
        Assert.Equal(["5.6.7.8"], Values("X-Forwarded-For"));
        Assert.Equal(["https"], Values("X-Forwarded-Proto"));
        Assert.Equal([$"mysite.{Domain}"], Values("X-Forwarded-Host"));
        Assert.Empty(Values("Connection"));
        Assert.Empty(Values("X-Secret"));
        Assert.Equal(["text/html"], Values("Accept"));
    }

    [Fact]
    [Trait("Rule", "SITE-08")]
    public async Task SITE08_site_paths_are_the_sites_and_get_no_dalan_headers()
    {
        await using var app = await StartAsync(postgres);
        var (_, _, key) = await app.BuilderAsync();
        var agent = app.Agent(key);
        var session = await agent.SessionAsync("mysite");

        var poll = agent.PollAsync(session);
        var call = app.Visitor("mysite").GetAsync("/api/me", Cancel);
        var request = Assert.Single(await poll);
        await agent.AnswerAsync(session, request.GetProperty("id").GetInt64(), 200, "{\"site\":true}");
        var response = await call;

        Assert.Equal("/api/me", request.GetProperty("path").GetString());
        Assert.Equal("{\"site\":true}", await response.Content.ReadAsStringAsync(Cancel));
        Assert.False(response.Headers.Contains("Content-Security-Policy"));
        Assert.False(response.Headers.Contains("Referrer-Policy"));
    }

    [Fact]
    [Trait("Rule", "SITE-09")]
    public async Task SITE09_site_without_agent_is_offline_and_unknown_name_is_not_found()
    {
        await using var app = await StartAsync(postgres);
        var (_, _, key) = await app.BuilderAsync();
        await app.Agent(key).SessionAsync("mysite");
        // The agent never reads: after the idle time the site is offline.
        app.Time.Advance(TimeSpan.FromSeconds(61));

        var offline = await app.Visitor("mysite").GetAsync("/", Cancel);
        var unknown = await app.Visitor("nothing").GetAsync("/", Cancel);

        Assert.Equal(HttpStatusCode.BadGateway, offline.StatusCode);
        Assert.Equal("site_offline", offline.SiteError());
        Assert.Contains("site_offline", await offline.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("site_not_found", unknown.SiteError());
    }

    [Fact]
    [Trait("Rule", "SITE-10")]
    public async Task SITE10_request_with_no_answer_in_time_gets_504()
    {
        await using var app = await StartAsync(postgres);
        var (_, _, key) = await app.BuilderAsync();
        var agent = app.Agent(key);
        var session = await agent.SessionAsync("mysite");

        var poll = agent.PollAsync(session);
        var call = app.Visitor("mysite").GetAsync("/slow", Cancel);
        Assert.Single(await poll);
        app.Time.Advance(TimeSpan.FromSeconds(30));
        var response = await call;

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Equal("site_timeout", response.SiteError());
    }

    [Fact]
    [Trait("Rule", "SITE-11")]
    public async Task SITE11_body_over_the_limit_gets_413_and_never_reaches_the_agent()
    {
        await using var app = await StartAsync(postgres, ("Sites:MaxBodyMb", "1"));
        var (_, _, key) = await app.BuilderAsync();
        var agent = app.Agent(key);
        var session = await agent.SessionAsync("mysite");

        var response = await app.Visitor("mysite").PostAsync("/upload", new ByteArrayContent(new byte[1024 * 1024 + 1]), Cancel);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Empty(await agent.PollAsync(session, timeout: 0));
    }

    [Fact]
    [Trait("Rule", "SITE-11")]
    public async Task SITE11_answer_over_the_limit_gives_the_visitor_502()
    {
        await using var app = await StartAsync(postgres, ("Sites:MaxBodyMb", "1"));
        var (_, _, key) = await app.BuilderAsync();
        var agent = app.Agent(key);
        var session = await agent.SessionAsync("mysite");

        var poll = agent.PollAsync(session);
        var call = app.Visitor("mysite").GetAsync("/big", Cancel);
        var request = Assert.Single(await poll);
        await agent.AnswerAsync(session, request.GetProperty("id").GetInt64(), 200, new string('x', 1024 * 1024 + 1));

        Assert.Equal(HttpStatusCode.BadGateway, (await call).StatusCode);
    }

    [Fact]
    [Trait("Rule", "SITE-13")]
    public async Task SITE13_server_refuses_to_start_with_sites_under_dalans_domain()
    {
        await using var app = new DalanApp(await postgres.NewDatabaseAsync(), new Dictionary<string, string>
        {
            ["Admin:SiteUrl"] = "https://dalanet.ir",
            ["Sites:Domain"] = "sites.dalanet.ir",
        });

        var error = Assert.ThrowsAny<Exception>(() => app.CreateClient());

        Assert.Contains("Sites:Domain", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("Admin:SiteUrl", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "SITE-14")]
    public async Task SITE14_opening_a_session_writes_the_access_log()
    {
        await using var app = await StartAsync(postgres);
        var (_, userId, key) = await app.BuilderAsync();

        await app.Agent(key, "9.8.7.6:4000").SessionAsync("mysite");

        Assert.Contains($"User {userId} 9.8.7.6 4000", await app.AccessLogAsync());
    }

    [Fact]
    [Trait("Rule", "SITE-15")]
    public async Task SITE15_suspended_user_loses_the_websocket_and_new_calls()
    {
        await using var app = await StartAsync(postgres);
        var (browser, userId, key) = await app.BuilderAsync();
        var session = await app.Agent(key).SessionAsync("mysite");
        var client = app.Server.CreateWebSocketClient();
        client.ConfigureRequest = request => request.Headers.Authorization = "Bearer " + key;
        using var socket = await client.ConnectAsync(new Uri($"ws://localhost/api/tunnel/sessions/{session}/ws"), Cancel);
        var admin = await app.AdminAsync();

        (await admin.PostAsJsonAsync($"/api/admin/users/{userId}/suspend", new { suspended = true }, Cancel)).EnsureSuccessStatusCode();
        var refused = await app.Agent(key).OpenAsync("mysite");
        app.Time.Advance(TimeSpan.FromSeconds(60));
        var closed = await socket.ReceiveAsync(new byte[1024], Cancel);
        var visitor = await app.Visitor("mysite").GetAsync("/", Cancel);

        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        Assert.Equal(System.Net.WebSockets.WebSocketMessageType.Close, closed.MessageType);
        Assert.Equal("site_offline", visitor.SiteError());
        _ = browser;
    }

    [Fact]
    [Trait("Rule", "SITE-16")]
    public async Task SITE16_builder_lists_and_deletes_a_site()
    {
        await using var app = await StartAsync(postgres);
        var (browser, _, key) = await app.BuilderAsync();
        var agent = app.Agent(key);
        var session = await agent.SessionAsync("mysite");
        var poll = agent.PollAsync(session);

        var list = await browser.GetFromJsonAsync<JsonElement>("/api/sites", Cancel);
        var site = Assert.Single(list.GetProperty("sites").EnumerateArray());
        var deleted = await browser.DeleteAsync($"/api/sites/{site.GetProperty("id").GetInt64()}", Cancel);
        var after = await browser.GetFromJsonAsync<JsonElement>("/api/sites", Cancel);
        var visitor = await app.Visitor("mysite").GetAsync("/", Cancel);

        Assert.Equal("mysite", site.GetProperty("name").GetString());
        Assert.Equal($"https://mysite.{Domain}", site.GetProperty("url").GetString());
        Assert.True(site.GetProperty("online").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(after.GetProperty("sites").EnumerateArray());
        Assert.Equal(Domain, list.GetProperty("domain").GetString());
        Assert.Equal("https://tunnel.test", list.GetProperty("tunnel_url").GetString());
        Assert.Equal(HttpStatusCode.NotFound, visitor.StatusCode);
        var ended = await agent.GetAsync($"/api/tunnel/sessions/{session}/requests?timeout=0", Cancel);
        Assert.Equal(HttpStatusCode.NotFound, ended.StatusCode);
        _ = poll;
    }

    [Fact]
    [Trait("Rule", "SITE-16")]
    public async Task SITE16_builder_cannot_delete_a_site_of_another()
    {
        await using var app = await StartAsync(postgres);
        var (owner, _, key) = await app.BuilderAsync("A");
        var (other, _, _) = await app.BuilderAsync("B");
        await app.Agent(key).SessionAsync("mysite");
        var id = (await owner.GetFromJsonAsync<JsonElement>("/api/sites", Cancel)).GetProperty("sites")[0].GetProperty("id").GetInt64();

        var response = await other.DeleteAsync($"/api/sites/{id}", Cancel);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Single((await owner.GetFromJsonAsync<JsonElement>("/api/sites", Cancel)).GetProperty("sites").EnumerateArray());
    }

    [Fact]
    [Trait("Rule", "SITE-13")]
    public async Task SITE13_admin_panel_changes_site_limits_but_not_the_sites_domain()
    {
        await using var app = await StartAsync(postgres);
        var admin = await app.AdminAsync();

        var settings = await admin.GetFromJsonAsync<JsonElement>("/api/admin/settings", Cancel);
        var keys = settings.GetProperty("settings").EnumerateArray().Select(s => s.GetProperty("key").GetString()).ToList();

        Assert.Contains("Sites:MaxBodyMb", keys);
        Assert.Contains("Sites:MaxPendingPerAddress", keys);
        Assert.DoesNotContain("Sites:Domain", keys);
    }
}
