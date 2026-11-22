using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dalan.Agent;
using Dalan.Sites.Tunnel;
using Dalan.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using static Dalan.Sites.Tests.SitesApp;

namespace Dalan.Sites.Tests;

/// <summary>Demo links: the address on the demo host (SITE-18 to SITE-20) and the agent command (AGT-11).</summary>
public partial class DemoTests(PostgresServer postgres)
{
    private const string DemoHost = "demo.test";

    private Task<DalanApp> StartDemoAsync(params (string Key, string Value)[] more) =>
        StartAsync(postgres, [("Sites:DemoHost", DemoHost), .. more]);

    private static Task<HttpResponseMessage> OpenDemoAsync(HttpClient agent, string demo = "new") =>
        agent.PostAsJsonAsync("/api/tunnel/sessions", new { demo }, Cancel);

    private static async Task<(string Session, string Id, JsonElement Demo)> NewDemoAsync(HttpClient agent, string demo = "new")
    {
        var response = await OpenDemoAsync(agent, demo);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancel);
        var address = body.GetProperty("demo");
        return (body.GetProperty("session").GetString()!, address.GetProperty("id").GetString()!, address);
    }

    private static HttpClient DemoVisitor(DalanApp app, string? cookie = null)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri($"https://{DemoHost}"),
            AllowAutoRedirect = false,
        });
        client = DalanApp.From(client, "5.6.7.8:1234");
        if (cookie is not null) client.DefaultRequestHeaders.Add("Cookie", cookie);
        return client;
    }

    [Fact]
    [Trait("Rule", "SITE-18")]
    public async Task SITE18_new_demo_gets_a_random_id_an_address_and_an_end_in_24_hours()
    {
        await using var app = await StartDemoAsync();
        var (_, userId, key) = await app.BuilderAsync();

        var (_, id, demo) = await NewDemoAsync(app.Agent(key));

        Assert.Matches("^[a-z0-9]{14}$", id);
        Assert.Equal($"https://{DemoHost}/{id}/", demo.GetProperty("url").GetString());
        Assert.Equal(app.Time.GetUtcNow().AddHours(24), demo.GetProperty("expires_at").GetDateTimeOffset());
        Assert.Equal(userId, await PostgresServer.ScalarAsync<long>(app.ConnectionString, $"SELECT owner_user_id FROM sites.demos WHERE id = '{id}'"));
    }

    [Fact]
    [Trait("Rule", "SITE-18")]
    public async Task SITE18_demos_are_off_when_the_demo_host_is_empty()
    {
        await using var app = await StartAsync(postgres, ("Sites:DemoHost", ""));
        var (_, _, key) = await app.BuilderAsync();

        var response = await OpenDemoAsync(app.Agent(key));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("demos_off", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "SITE-18")]
    public async Task SITE18_fourth_live_demo_of_a_builder_is_refused()
    {
        await using var app = await StartDemoAsync();
        var (_, _, key) = await app.BuilderAsync();
        var agent = app.Agent(key);
        for (var i = 0; i < 3; i++) await NewDemoAsync(agent);

        var response = await OpenDemoAsync(agent);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("demo_limit", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "SITE-18")]
    public async Task SITE18_ended_demos_do_not_count_for_the_limit_and_their_rows_stay()
    {
        await using var app = await StartDemoAsync();
        var (_, _, key) = await app.BuilderAsync();
        var agent = app.Agent(key);
        var (_, first, _) = await NewDemoAsync(agent);
        await NewDemoAsync(agent);
        await NewDemoAsync(agent);
        (await agent.DeleteAsync($"/api/tunnel/demos/{first}", Cancel)).EnsureSuccessStatusCode();

        await NewDemoAsync(agent);

        Assert.Equal(4, await PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT count(*) FROM sites.demos"));
    }

    [Fact]
    [Trait("Rule", "SITE-18")]
    public async Task SITE18_agent_that_lost_its_connection_gets_its_demo_back_and_another_builder_gets_404()
    {
        await using var app = await StartDemoAsync();
        var (_, _, key) = await app.BuilderAsync("A");
        var (_, _, other) = await app.BuilderAsync("B");
        var (_, id, _) = await NewDemoAsync(app.Agent(key));

        var (session, again, _) = await NewDemoAsync(app.Agent(key), id);
        var stranger = await OpenDemoAsync(app.Agent(other), id);
        var visitor = DemoVisitor(app).GetAsync($"/{id}/", Cancel);
        var request = Assert.Single(await app.Agent(key).PollAsync(session));
        await app.Agent(key).AnswerAsync(session, request.GetProperty("id").GetInt64(), 200, "back");

        Assert.Equal(id, again);
        Assert.Equal("back", await (await visitor).Content.ReadAsStringAsync(Cancel));
        Assert.Equal(HttpStatusCode.NotFound, stranger.StatusCode);
        Assert.Equal("demo_not_found", await stranger.ErrorCodeAsync());
        Assert.Equal(1, await PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT count(*) FROM sites.demos"));
    }

    [Fact]
    [Trait("Rule", "SITE-19")]
    public async Task SITE19_first_visit_goes_to_the_demo_without_its_id_and_sets_the_cookie()
    {
        await using var app = await StartDemoAsync();
        var (_, _, key) = await app.BuilderAsync();
        var agent = app.Agent(key);
        var (session, id, _) = await NewDemoAsync(agent);

        var visitor = DemoVisitor(app).GetAsync($"/{id}/page?x=1", Cancel);
        var request = Assert.Single(await agent.PollAsync(session));
        await agent.AnswerAsync(session, request.GetProperty("id").GetInt64(), 200, "page");
        var response = await visitor;

        Assert.Equal($"demo:{id}", request.GetProperty("site").GetString());
        Assert.Equal("/page?x=1", request.GetProperty("path").GetString());
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith($"dalan_demo={id};", cookie, StringComparison.Ordinal);
        foreach (var part in new[] { "path=/", "secure", "httponly", "samesite=lax" })
            Assert.Contains(part, cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Rule", "SITE-19")]
    public async Task SITE19_id_without_a_slash_goes_to_the_address_with_a_slash()
    {
        await using var app = await StartDemoAsync();
        var (_, _, key) = await app.BuilderAsync();
        var (_, id, _) = await NewDemoAsync(app.Agent(key));

        var response = await DemoVisitor(app).GetAsync($"/{id}?a=b", Cancel);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/{id}/?a=b", response.Headers.Location?.OriginalString);
    }

    [Fact]
    [Trait("Rule", "SITE-19")]
    public async Task SITE19_link_of_the_app_with_the_cookie_goes_to_the_demo_unchanged()
    {
        await using var app = await StartDemoAsync();
        var (_, _, key) = await app.BuilderAsync();
        var agent = app.Agent(key);
        var (session, id, _) = await NewDemoAsync(agent);

        var visitor = DemoVisitor(app, $"dalan_demo={id}").GetAsync("/app.js", Cancel);
        var request = Assert.Single(await agent.PollAsync(session));
        await agent.AnswerAsync(session, request.GetProperty("id").GetInt64(), 200, "js");

        Assert.Equal("/app.js", request.GetProperty("path").GetString());
        Assert.Equal("js", await (await visitor).Content.ReadAsStringAsync(Cancel));
    }

    [Theory]
    [Trait("Rule", "SITE-19")]
    [InlineData(null)]
    [InlineData("dalan_demo=nosuchdemo0000")]
    public async Task SITE19_no_demo_or_an_unknown_cookie_gets_the_404_page(string? cookie)
    {
        await using var app = await StartDemoAsync();

        var response = await DemoVisitor(app, cookie).GetAsync("/app.js", Cancel);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("demo_not_found", response.SiteError());
    }

    [Fact]
    [Trait("Rule", "SITE-20")]
    public async Task SITE20_demo_whose_time_is_over_answers_410_and_the_agent_gets_nothing()
    {
        await using var app = await StartDemoAsync();
        var (_, _, key) = await app.BuilderAsync();
        var agent = app.Agent(key);
        var (session, id, _) = await NewDemoAsync(agent);

        app.Time.Advance(TimeSpan.FromHours(24) + TimeSpan.FromSeconds(1));
        var byAddress = await DemoVisitor(app).GetAsync($"/{id}/", Cancel);
        var byCookie = await DemoVisitor(app, $"dalan_demo={id}").GetAsync("/app.js", Cancel);
        var poll = await agent.GetAsync($"/api/tunnel/sessions/{session}/requests?timeout=0", Cancel);
        var again = await OpenDemoAsync(agent, id);

        Assert.Equal(HttpStatusCode.Gone, byAddress.StatusCode);
        Assert.Equal("demo_ended", byAddress.SiteError());
        Assert.Equal(HttpStatusCode.Gone, byCookie.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, poll.StatusCode);
        Assert.Equal(HttpStatusCode.Gone, again.StatusCode);
    }

    [Fact]
    [Trait("Rule", "SITE-20")]
    public async Task SITE20_owner_ends_the_demo_at_once_and_nobody_else_can()
    {
        await using var app = await StartDemoAsync();
        var (_, _, key) = await app.BuilderAsync("A");
        var (_, _, other) = await app.BuilderAsync("B");
        var (_, id, _) = await NewDemoAsync(app.Agent(key));

        var foreign = await app.Agent(other).DeleteAsync($"/api/tunnel/demos/{id}", Cancel);
        var stillLive = await DemoVisitor(app).GetAsync($"/{id}", Cancel);
        var ended = await app.Agent(key).DeleteAsync($"/api/tunnel/demos/{id}", Cancel);
        var response = await DemoVisitor(app).GetAsync($"/{id}/", Cancel);

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, stillLive.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, ended.StatusCode);
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Equal("demo_ended", response.SiteError());
        Assert.Equal(1, await PostgresServer.ScalarAsync<long>(app.ConnectionString, $"SELECT count(*) FROM sites.demos WHERE id = '{id}' AND ended_at IS NOT NULL"));
    }

    [Fact]
    [Trait("Rule", "AGT-11")]
    public void AGT11_command_line_reads_demo_and_one_port()
    {
        var command = CommandLine.Parse(["demo", "3000"]);

        Assert.Equal(CommandKind.Demo, command.Kind);
        Assert.Equal([(3000, (string?)null)], command.Sites);
        Assert.Throws<AgentStopException>(() => CommandLine.Parse(["demo"]));
        Assert.Throws<AgentStopException>(() => CommandLine.Parse(["demo", "port"]));
        Assert.Throws<AgentStopException>(() => CommandLine.Parse(["demo", "3000", "4000"]));
    }

    [Fact]
    [Trait("Rule", "AGT-11")]
    public async Task AGT11_start_prints_the_address_end_and_warning_then_stop_ends_the_demo()
    {
        await using var app = await StartDemoAsync();
        var (_, _, key) = await app.BuilderAsync();
        var output = new StringWriter();
        var local = FakeLocalApp.Text("demo app");
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        var agent = app.RealAgent(key, local, TextWriter.Synchronized(output));

        var run = agent.RunAsync([], stop.Token, demoPort: 3000);
        var id = await WaitForDemoIdAsync(output);
        var page = await DemoVisitor(app).GetStringAsync($"/{id}/", Cancel);
        await stop.CancelAsync();
        await run;
        var after = await DemoVisitor(app).GetAsync($"/{id}/", Cancel);

        var text = output.ToString();
        Assert.Equal("demo app", page);
        Assert.Equal("/", Assert.Single(local.Got).Request.RequestUri?.AbsolutePath);
        Assert.Contains($"https://{DemoHost}/{id}/", text, StringComparison.Ordinal);
        Assert.Contains("UTC", text, StringComparison.Ordinal);
        Assert.Contains("A demo is for tests: do not use real accounts or secrets in it", text, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Gone, after.StatusCode);
    }

    [Fact]
    [Trait("Rule", "AGT-11")]
    public async Task AGT11_agent_that_loses_its_session_keeps_the_same_demo()
    {
        await using var app = await StartDemoAsync();
        var (_, _, key) = await app.BuilderAsync();
        var output = new StringWriter();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        var agent = app.RealAgent(key, FakeLocalApp.Text("demo app"), TextWriter.Synchronized(output));
        var hub = app.Services.GetRequiredService<TunnelHub>();

        var run = agent.RunAsync([], stop.Token, demoPort: 3000);
        var id = await WaitForDemoIdAsync(output);
        var first = hub.Find($"demo:{id}")!;
        hub.End(first);
        for (var i = 0; i < 500 && (hub.Find($"demo:{id}") is null || hub.Find($"demo:{id}") == first); i++) await Task.Delay(10, Cancel);
        var page = await DemoVisitor(app).GetStringAsync($"/{id}/", Cancel);
        await stop.CancelAsync();
        await run;

        Assert.Equal("demo app", page);
        Assert.Equal(1, await PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT count(*) FROM sites.demos"));
    }

    [Fact]
    [Trait("Rule", "AGT-11")]
    public async Task AGT11_agent_exits_with_a_message_when_the_server_says_the_demo_ended()
    {
        await using var app = await StartDemoAsync();
        var (_, _, key) = await app.BuilderAsync();
        var output = new StringWriter();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        var agent = app.RealAgent(key, FakeLocalApp.Text("demo app"), TextWriter.Synchronized(output));

        var run = agent.RunAsync([], stop.Token, demoPort: 3000);
        var id = await WaitForDemoIdAsync(output);
        (await app.Agent(key).DeleteAsync($"/api/tunnel/demos/{id}", Cancel)).EnsureSuccessStatusCode();

        var stopped = await Assert.ThrowsAsync<AgentStopException>(() => run);
        Assert.Contains("ended", stopped.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> WaitForDemoIdAsync(StringWriter output)
    {
        for (var i = 0; i < 500; i++)
        {
            var match = DemoAddress().Match(output.ToString());
            if (match.Success) return match.Groups[1].Value;
            await Task.Delay(10, Cancel);
        }

        Assert.Fail("the agent did not print a demo address");
        return "";
    }

    [GeneratedRegex(@"https://demo\.test/([a-z0-9]{14})/")]
    private static partial Regex DemoAddress();
}
