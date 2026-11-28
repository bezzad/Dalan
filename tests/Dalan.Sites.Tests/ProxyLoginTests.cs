using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Agent;
using Dalan.Testing;
using Microsoft.AspNetCore.WebUtilities;
using static Dalan.Sites.Tests.SitesApp;

namespace Dalan.Sites.Tests;

/// <summary>SITE-26: a site may ask Dalan to log the visitor in on the proxy; the site gets the user in headers.</summary>
public class ProxyLoginTests(PostgresServer postgres)
{
    private const string Issuer = "https://dalanet.ir";

    private static Task<DalanApp> StartLoginAsync(PostgresServer postgres) => StartAsync(postgres, ("Admin:SiteUrl", Issuer));

    /// <summary>A builder with an online site "shop" whose proxy asks for login. Returns the agent and its session.</summary>
    private static async Task<(HttpClient Builder, HttpClient Agent, string Session)> ShopAsync(DalanApp app, bool login = true)
    {
        var (builder, _, key) = await app.BuilderAsync();
        var agent = app.Agent(key);
        var session = await agent.SessionAsync("shop");
        if (login)
        {
            var id = (await builder.GetFromJsonAsync<JsonElement>("/api/sites", Cancel)).GetProperty("sites")[0].GetProperty("id").GetInt64();
            var response = await builder.PutAsJsonAsync($"/api/sites/{id}/login", new { on = true }, Cancel);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>(Cancel)).GetProperty("login").GetBoolean());
        }

        return (builder, agent, session);
    }

    /// <summary>Dalan's login page: the visitor, logged in on Dalan, allows the site. Returns the address back to the site.</summary>
    private static async Task<Uri> AllowAsync(HttpClient dalan, Uri authorize)
    {
        Uri? back = null;
        foreach (var answer in (string?[])[null, "allow"])
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/authorize")
            {
                Content = JsonContent.Create(new { query = authorize.Query, answer, email = false }),
            };
            request.Headers.Add("Origin", "https://localhost");
            var response = await dalan.SendAsync(request, Cancel);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancel);
            if (answer is null)
            {
                // The consent screen names the site and what it gets.
                var consent = body.GetProperty("consent");
                Assert.Equal($"https://shop.{Domain}", consent.GetProperty("url").GetString());
                Assert.Equal("Sara", consent.GetProperty("name").GetString());
            }
            else back = new Uri(body.GetProperty("redirect").GetString()!);
        }

        return back!;
    }

    /// <summary>The visitor opens the page, logs in on Dalan and comes back with the login cookie.</summary>
    private static async Task<HttpClient> LoggedInVisitorAsync(DalanApp app, HttpClient dalan, string path = "/cart?x=1")
    {
        var visitor = app.Visitor("shop");
        var first = await visitor.GetAsync(path, Cancel);
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        var authorize = first.Headers.Location!;
        Assert.StartsWith(Issuer + "/oauth/authorize?", authorize.ToString());
        var query = QueryHelpers.ParseQuery(authorize.Query);
        Assert.Equal("shop", query["client_id"].ToString());
        Assert.Equal($"https://shop.{Domain}/.dalan/callback", query["redirect_uri"].ToString());
        Assert.Equal("S256", query["code_challenge_method"].ToString());

        var back = await AllowAsync(dalan, authorize);
        Assert.Equal($"https://shop.{Domain}/.dalan/callback", back.GetLeftPart(UriPartial.Path));
        var callback = await visitor.GetAsync(back.PathAndQuery, Cancel);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal(path, callback.Headers.Location!.OriginalString);
        var cookie = Assert.Single(callback.Headers.GetValues("Set-Cookie"), c => c.StartsWith("__Host-dalan_login=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
        return visitor;
    }

    [Fact]
    [Trait("Rule", "SITE-26")]
    public async Task SITE26_visitor_with_no_login_logs_in_and_the_site_gets_the_user()
    {
        await using var app = await StartLoginAsync(postgres);
        var (_, agent, session) = await ShopAsync(app);
        var (dalan, _) = await app.LoginAsync("Sara");

        var visitor = await LoggedInVisitorAsync(app, dalan);
        var page = visitor.GetAsync("/cart?x=1", Cancel);
        var request = Assert.Single(await agent.PollAsync(session));
        await agent.AnswerAsync(session, request.GetProperty("id").GetInt64(), 200, "cart");

        Assert.Equal("cart", await (await page).Content.ReadAsStringAsync(Cancel));
        Assert.Equal("/cart?x=1", request.GetProperty("path").GetString());
        var headers = request.GetProperty("headers");
        var subject = headers.GetProperty("X-Dalan-User")[0].GetString()!;
        Assert.False(string.IsNullOrEmpty(subject));
        Assert.Equal("Sara", Uri.UnescapeDataString(headers.GetProperty("X-Dalan-Name")[0].GetString()!));
        // The proxy's own cookies never reach the site.
        Assert.False(headers.TryGetProperty("Cookie", out var cookies) && cookies.EnumerateArray().Any(c => c.GetString()!.Contains("__Host-dalan")));
    }

    [Fact]
    [Trait("Rule", "SITE-26")]
    public async Task SITE26_forged_header_never_reaches_the_site()
    {
        await using var app = await StartLoginAsync(postgres);
        var (_, agent, session) = await ShopAsync(app);
        var (dalan, _) = await app.LoginAsync("Sara");
        var visitor = await LoggedInVisitorAsync(app, dalan);

        using var forged = new HttpRequestMessage(HttpMethod.Get, "/cart");
        forged.Headers.Add("X-Dalan-User", "admin");
        forged.Headers.Add("x-dalan-name", "Admin");
        var page = visitor.SendAsync(forged, Cancel);
        var request = Assert.Single(await agent.PollAsync(session));
        await agent.AnswerAsync(session, request.GetProperty("id").GetInt64(), 200, "ok");
        await page;

        var users = request.GetProperty("headers").EnumerateObject()
            .Where(h => h.Name.StartsWith("X-Dalan-", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Equal(2, users.Count);
        Assert.DoesNotContain(users, h => h.Value[0].GetString() is "admin" or "Admin");
    }

    [Fact]
    [Trait("Rule", "SITE-26")]
    public async Task SITE26_forged_header_is_dropped_also_when_the_site_asks_no_login()
    {
        await using var app = await StartLoginAsync(postgres);
        var (_, agent, session) = await ShopAsync(app, login: false);

        using var forged = new HttpRequestMessage(HttpMethod.Get, "/");
        forged.Headers.Add("X-Dalan-User", "admin");
        var page = app.Visitor("shop").SendAsync(forged, Cancel);
        var request = Assert.Single(await agent.PollAsync(session));
        await agent.AnswerAsync(session, request.GetProperty("id").GetInt64(), 200, "ok");
        await page;

        Assert.False(request.GetProperty("headers").TryGetProperty("X-Dalan-User", out _));
    }

    [Fact]
    [Trait("Rule", "SITE-26")]
    public async Task SITE26_call_that_is_not_a_page_load_gets_401_and_no_request_reaches_the_site()
    {
        await using var app = await StartLoginAsync(postgres);
        var (_, agent, session) = await ShopAsync(app);

        var response = await app.Visitor("shop").PostAsync("/api/cart", new StringContent("{}"), Cancel);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("login_required", response.SiteError());
        Assert.Empty(await agent.PollAsync(session, timeout: 0));
    }

    [Fact]
    [Trait("Rule", "SITE-26")]
    public async Task SITE26_callback_with_a_wrong_state_or_no_state_cookie_sets_no_login()
    {
        await using var app = await StartLoginAsync(postgres);
        await ShopAsync(app);
        var (dalan, _) = await app.LoginAsync("Sara");
        var visitor = app.Visitor("shop");
        var authorize = (await visitor.GetAsync("/", Cancel)).Headers.Location!;
        var back = await AllowAsync(dalan, authorize);
        var code = QueryHelpers.ParseQuery(back.Query)["code"].ToString();

        var wrong = await visitor.GetAsync($"/.dalan/callback?code={code}&state=other", Cancel);
        var stranger = await app.Visitor("shop").GetAsync(back.PathAndQuery, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal("login_failed", wrong.SiteError());
        Assert.Equal(HttpStatusCode.BadRequest, stranger.StatusCode);
        Assert.False(stranger.Headers.TryGetValues("Set-Cookie", out var set) && set.Any(c => c.StartsWith("__Host-dalan_login=", StringComparison.Ordinal)));
    }

    [Fact]
    [Trait("Rule", "SITE-26")]
    public async Task SITE26_login_cookie_of_one_site_does_not_open_another()
    {
        await using var app = await StartLoginAsync(postgres);
        await ShopAsync(app);
        var (dalan, _) = await app.LoginAsync("Sara");
        var cookie = await CookieAsync(app, dalan);

        // The same cookie sent to another site's host is not a login there.
        var (builder, _, key) = await app.BuilderAsync("Other");
        await app.Agent(key).SessionAsync("blog");
        var id = (await builder.GetFromJsonAsync<JsonElement>("/api/sites", Cancel)).GetProperty("sites")[0].GetProperty("id").GetInt64();
        await builder.PutAsJsonAsync($"/api/sites/{id}/login", new { on = true }, Cancel);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Cookie", cookie);
        var blog = await app.Visitor("blog").SendAsync(request, Cancel);

        Assert.Equal(HttpStatusCode.Redirect, blog.StatusCode);
        Assert.Contains("client_id=blog", blog.Headers.Location!.Query);
    }

    /// <summary>The login cookie of "shop" as "name=value".</summary>
    private static async Task<string> CookieAsync(DalanApp app, HttpClient dalan)
    {
        var visitor = app.Visitor("shop");
        var authorize = (await visitor.GetAsync("/", Cancel)).Headers.Location!;
        var back = await AllowAsync(dalan, authorize);
        var callback = await visitor.GetAsync(back.PathAndQuery, Cancel);
        return callback.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("__Host-dalan_login=", StringComparison.Ordinal)).Split(';')[0];
    }

    [Fact]
    [Trait("Rule", "SITE-26")]
    public async Task SITE26_logout_ends_the_login()
    {
        await using var app = await StartLoginAsync(postgres);
        await ShopAsync(app);
        var (dalan, _) = await app.LoginAsync("Sara");
        var visitor = await LoggedInVisitorAsync(app, dalan);

        var logout = await visitor.GetAsync("/.dalan/logout", Cancel);
        var again = await visitor.GetAsync("/cart", Cancel);

        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Contains(logout.Headers.GetValues("Set-Cookie"), c => c.StartsWith("__Host-dalan_login=;", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.Redirect, again.StatusCode);
        Assert.StartsWith(Issuer + "/oauth/authorize?", again.Headers.Location!.ToString());
    }

    [Fact]
    [Trait("Rule", "SITE-26")]
    public async Task SITE26_turning_it_off_lets_visitors_in_with_no_login()
    {
        await using var app = await StartLoginAsync(postgres);
        var (builder, agent, session) = await ShopAsync(app);
        var id = (await builder.GetFromJsonAsync<JsonElement>("/api/sites", Cancel)).GetProperty("sites")[0].GetProperty("id").GetInt64();

        await builder.PutAsJsonAsync($"/api/sites/{id}/login", new { on = false }, Cancel);
        var page = app.Visitor("shop").GetAsync("/", Cancel);
        var request = Assert.Single(await agent.PollAsync(session));
        await agent.AnswerAsync(session, request.GetProperty("id").GetInt64(), 200, "open");

        Assert.Equal(HttpStatusCode.OK, (await page).StatusCode);
        Assert.Equal(1, await PostgresServer.ScalarAsync<long>(app.ConnectionString,
            "SELECT count(*) FROM admin.audit_log WHERE action = 'site.login.off'"));
    }

    [Fact]
    [Trait("Rule", "SITE-26")]
    public void SITE26_agent_reads_the_login_switch()
    {
        var command = CommandLine.Parse(["http", "3000", "--name", "shop", "--login"]);

        Assert.True(command.Login);
        Assert.Equal([(3000, "shop")], command.Sites);
        Assert.False(CommandLine.Parse(["http", "3000"]).Login);
    }

    [Fact]
    [Trait("Rule", "SITE-26")]
    public async Task SITE26_dalan_http_login_turns_it_on()
    {
        await using var app = await StartLoginAsync(postgres);
        var (builder, _, key) = await app.BuilderAsync();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        var run = new TunnelClient(app.CreateClient(), key, (_, _, _) => throw new System.Net.WebSockets.WebSocketException("off"),
            new LocalForwarder(FakeLocalApp.Text("hello")), TextWriter.Null) { FirstWait = TimeSpan.FromMilliseconds(20), Login = true }
            .RunAsync([new SiteEntry("shop", 3000)], stop.Token);

        await builder.WaitOnlineAsync("shop");
        var first = await app.Visitor("shop").GetAsync("/", Cancel);
        await stop.CancelAsync();
        await run;

        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        Assert.True((await builder.GetFromJsonAsync<JsonElement>("/api/sites", Cancel)).GetProperty("sites")[0].GetProperty("login").GetBoolean());
    }
}
