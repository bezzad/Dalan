using System.Net;
using System.Net.Http.Json;
using System.Text;
using Dalan.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using static Dalan.Identity.Tests.Http;

namespace Dalan.Identity.Tests;

/// <summary>Level 1 login with GitHub, against a fake GitHub.</summary>
public class GitHubLoginTests(PostgresServer postgres)
{
    private const string StateCookie = "__Host-dalan_github";
    private const string Callback = "https://dalan.test/api/auth/github/callback";

    /// <summary>Plays github.com and api.github.com. A test changes the answers before the browser comes back.</summary>
    private sealed class FakeGitHub : HttpMessageHandler
    {
        public string Token { get; set; } = """{"access_token":"gho_test","token_type":"bearer"}""";
        public string User { get; set; } = """{"id":42,"login":"sara","name":"Sara Rad"}""";
        public string Emails { get; set; } = """[{"email":"old@example.com","primary":false,"verified":true},{"email":"Sara@Example.com","primary":true,"verified":true}]""";
        public Exception? Down { get; set; }

        /// <summary>The next calls that get no answer, as when the link from Iran stalls.</summary>
        public int StallCalls { get; set; }
        public List<string> Calls { get; } = [];
        public string TokenRequest { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!;
            Calls.Add($"{url.Host}{url.AbsolutePath}");
            if (Down is not null) throw Down;
            if (StallCalls > 0)
            {
                StallCalls--;
                throw new TaskCanceledException("no answer in time");
            }

            string body;
            switch (url.AbsolutePath)
            {
                case "/login/oauth/access_token":
                    TokenRequest = await request.Content!.ReadAsStringAsync(cancellationToken);
                    body = Token;
                    break;
                case "/user":
                    Assert.Equal("Bearer gho_test", request.Headers.Authorization?.ToString());
                    body = User;
                    break;
                case "/user/emails":
                    body = Emails;
                    break;
                default:
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private async Task<(DalanApp App, FakeGitHub GitHub)> StartAsync(bool on = true)
    {
        var github = new FakeGitHub();
        var app = new DalanApp(
            await postgres.NewDatabaseAsync(),
            new Dictionary<string, string>
            {
                ["Identity:GitHub:ClientId"] = on ? "client-1" : "",
                ["Identity:GitHub:ClientSecret"] = on ? "secret-1" : "",
                ["Identity:GitHub:CallbackUrl"] = Callback,
                ["Identity:GitHub:WebUrl"] = "https://github.test",
                ["Identity:GitHub:ApiUrl"] = "https://api.github.test",
            },
            web => web.ConfigureServices(services =>
                services.ConfigureHttpClientDefaults(client => client.ConfigurePrimaryHttpMessageHandler(() => github))));
        await app.ReadyAsync();
        return (app, github);
    }

    /// <summary>A browser that keeps cookies and does not follow redirects, so the test sees each 302.</summary>
    private static HttpClient Browser(DalanApp app, string? ip = null)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        if (ip is null) return client;
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
        return DalanApp.From(client, "127.0.0.1:40000");
    }

    /// <summary>Opens the start address and returns the state that went to GitHub.</summary>
    private static async Task<string> StartLoginAsync(HttpClient browser)
    {
        var response = await browser.GetAsync("/api/auth/github", Cancel);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return QueryHelpers.ParseQuery(response.Headers.Location!.Query)["state"].ToString();
    }

    private static Task<HttpResponseMessage> ComeBackAsync(HttpClient browser, string state, string extra = "code=abc") =>
        browser.GetAsync($"/api/auth/github/callback?{extra}&state={Uri.EscapeDataString(state)}", Cancel);

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient browser) =>
        await ComeBackAsync(browser, await StartLoginAsync(browser));

    private static string? Location(HttpResponseMessage response) => response.Headers.Location?.OriginalString;

    private static Task<long> CountUsersAsync(DalanApp app) =>
        PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT count(*) FROM identity.users");

    // ---- AUTH-01 ----

    [Fact]
    [Trait("Rule", "AUTH-01")]
    public async Task AUTH01_same_github_account_twice_fails()
    {
        var (app, _) = await StartAsync();
        await using var _app = app;
        await PostgresServer.ScalarAsync<int>(app.ConnectionString,
            "INSERT INTO identity.users (email, github_id, display_name, created_at) VALUES ('a@example.com', 42, 'A', now()) RETURNING 1");

        await Assert.ThrowsAnyAsync<Exception>(() => PostgresServer.ScalarAsync<int>(app.ConnectionString,
            "INSERT INTO identity.users (email, github_id, display_name, created_at) VALUES ('b@example.com', 42, 'B', now()) RETURNING 1"));
    }

    // ---- AUTH-16 ----

    [Theory]
    [Trait("Rule", "AUTH-16")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AUTH16_settings_say_if_github_login_is_on(bool on)
    {
        var (app, _) = await StartAsync(on);
        await using var _app = app;

        var json = await JsonAsync(await Browser(app).GetAsync("/api/auth/settings", Cancel));

        Assert.Equal(on, json.GetProperty("github_login").GetBoolean());
    }

    // ---- AUTH-27 start ----

    [Fact]
    [Trait("Rule", "AUTH-27")]
    public async Task AUTH27_start_goes_to_github_with_a_state_cookie()
    {
        var (app, _) = await StartAsync();
        await using var _app = app;

        var response = await Browser(app).GetAsync("/api/auth/github", Cancel);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.Equal("https://github.test/login/oauth/authorize", location.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("client-1", query["client_id"]);
        Assert.Equal(Callback, query["redirect_uri"]);
        Assert.Equal("user:email", query["scope"]);
        var cookie = SetCookie(response, StateCookie)!;
        Assert.Equal(query["state"], CookieValue(response, StateCookie));
        Assert.Equal(43, query["state"].ToString().Length); // 256 bits in base64url
        foreach (var flag in (string[])["httponly", "secure", "samesite=lax", "path=/", "max-age=600"])
            Assert.Contains(flag, cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Rule", "AUTH-27")]
    public async Task AUTH27_off_is_404()
    {
        var (app, _) = await StartAsync(on: false);
        await using var _app = app;

        Assert.Equal(HttpStatusCode.NotFound, (await Browser(app).GetAsync("/api/auth/github", Cancel)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Browser(app).GetAsync("/api/auth/github/callback?code=a&state=b", Cancel)).StatusCode);
    }

    // ---- AUTH-28 come back ----

    [Fact]
    [Trait("Rule", "AUTH-28")]
    public async Task AUTH28_new_user_is_made_and_logged_in()
    {
        var (app, github) = await StartAsync();
        await using var _app = app;
        var browser = Browser(app);

        var response = await LoginAsync(browser);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", Location(response));
        Assert.NotNull(SetCookie(response, SessionCookie));
        Assert.NotNull(SetCookie(response, RefreshCookie));
        Assert.Contains("code=abc", github.TokenRequest, StringComparison.Ordinal);
        Assert.Contains("client_secret=secret-1", github.TokenRequest, StringComparison.Ordinal);
        var me = await JsonAsync(await browser.GetAsync("/api/me", Cancel));
        Assert.Equal("Sara Rad", me.GetProperty("name").GetString());
        Assert.Equal("sara@example.com", me.GetProperty("email").GetString());
        Assert.False(me.GetProperty("has_phone").GetBoolean());
        Assert.Equal(42, await PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT github_id FROM identity.users"));
    }

    [Fact]
    [Trait("Rule", "AUTH-28")]
    public async Task AUTH28_login_is_the_name_when_there_is_no_name_and_a_long_name_is_cut()
    {
        var (app, github) = await StartAsync();
        await using var _app = app;
        github.User = """{"id":42,"login":"sara","name":null}""";
        await LoginAsync(Browser(app));
        github.User = $$"""{"id":43,"login":"x","name":"{{new string('a', 100)}}"}""";
        github.Emails = """[{"email":"long@example.com","primary":true,"verified":true}]""";
        await LoginAsync(Browser(app));

        var names = await PostgresServer.ScalarAsync<string[]>(app.ConnectionString,
            "SELECT array_agg(display_name ORDER BY id) FROM identity.users");

        Assert.Equal(["sara", new string('a', 64)], names);
    }

    [Fact]
    [Trait("Rule", "AUTH-28")]
    public async Task AUTH28_same_email_as_an_email_user_is_the_same_user()
    {
        var (app, _) = await StartAsync();
        await using var _app = app;
        var byEmail = await app.LoginByEmailAsync("sara@example.com", "Sara");
        var id = (await JsonAsync(await byEmail.GetAsync("/api/me", Cancel))).GetProperty("id").GetInt64();
        var browser = Browser(app);

        await LoginAsync(browser);

        var me = await JsonAsync(await browser.GetAsync("/api/me", Cancel));
        Assert.Equal(id, me.GetProperty("id").GetInt64());
        Assert.Equal("Sara", me.GetProperty("name").GetString());
        Assert.Equal(1, await CountUsersAsync(app));
        Assert.Equal(42, await PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT github_id FROM identity.users"));
    }

    [Fact]
    [Trait("Rule", "AUTH-28")]
    public async Task AUTH28_known_github_account_logs_in_even_with_a_new_email()
    {
        var (app, github) = await StartAsync();
        await using var _app = app;
        await LoginAsync(Browser(app));
        github.Emails = """[{"email":"new@example.com","primary":true,"verified":true}]""";
        var browser = Browser(app);

        var response = await LoginAsync(browser);

        Assert.Equal("/", Location(response));
        Assert.Equal(1, await CountUsersAsync(app));
        Assert.Equal("sara@example.com", (await JsonAsync(await browser.GetAsync("/api/me", Cancel))).GetProperty("email").GetString());
    }

    [Fact]
    [Trait("Rule", "AUTH-28")]
    public async Task AUTH28_login_is_in_the_access_log_with_the_real_address()
    {
        var (app, _) = await StartAsync();
        await using var _app = app;

        await LoginAsync(Browser(app, "203.0.113.7:51000"));

        var id = await PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT id FROM identity.users");
        Assert.Equal([$"User {id} 203.0.113.7 51000"], await app.AccessLogAsync());
    }

    [Fact]
    [Trait("Rule", "AUTH-28")]
    public async Task AUTH28_github_token_is_not_logged_or_stored()
    {
        var (app, _) = await StartAsync();
        await using var _app = app;

        await LoginAsync(Browser(app));

        Assert.DoesNotContain(app.Logs.Lines, line => line.Text.Contains("gho_test", StringComparison.Ordinal));
        Assert.Equal(0, await PostgresServer.ScalarAsync<long>(app.ConnectionString,
            "SELECT count(*) FROM identity.users u WHERE u::text LIKE '%gho_test%'"));
    }

    // ---- AUTH-29 failures ----

    [Fact]
    [Trait("Rule", "AUTH-29")]
    public async Task AUTH29_wrong_or_missing_state_is_refused_before_github_is_called()
    {
        var (app, github) = await StartAsync();
        await using var _app = app;
        var browser = Browser(app);
        await StartLoginAsync(browser);

        var wrong = await ComeBackAsync(browser, "not-the-state");
        var noCookie = await ComeBackAsync(Browser(app), "any");

        Assert.Equal("/?login_error=github_state", Location(wrong));
        Assert.Equal("/?login_error=github_state", Location(noCookie));
        Assert.Null(SetCookie(wrong, SessionCookie));
        Assert.Empty(github.Calls);
    }

    [Fact]
    [Trait("Rule", "AUTH-29")]
    public async Task AUTH29_state_works_only_once()
    {
        var (app, _) = await StartAsync();
        await using var _app = app;
        var browser = Browser(app);
        var state = await StartLoginAsync(browser);

        var first = await ComeBackAsync(browser, state);
        var again = await ComeBackAsync(browser, state);

        Assert.Equal("/", Location(first));
        Assert.Equal("/?login_error=github_state", Location(again));
    }

    [Fact]
    [Trait("Rule", "AUTH-29")]
    public async Task AUTH29_person_said_no()
    {
        var (app, github) = await StartAsync();
        await using var _app = app;
        var browser = Browser(app);

        var response = await ComeBackAsync(browser, await StartLoginAsync(browser), "error=access_denied");

        Assert.Equal("/?login_error=github_denied", Location(response));
        Assert.Empty(github.Calls);
    }

    [Fact]
    [Trait("Rule", "AUTH-29")]
    public async Task AUTH29_no_verified_primary_email()
    {
        var (app, github) = await StartAsync();
        await using var _app = app;
        github.Emails = """[{"email":"sara@example.com","primary":true,"verified":false},{"email":"b@example.com","primary":false,"verified":true}]""";

        var response = await LoginAsync(Browser(app));

        Assert.Equal("/?login_error=github_no_email", Location(response));
        Assert.Null(SetCookie(response, SessionCookie));
        Assert.Equal(0, await CountUsersAsync(app));
    }

    public static TheoryData<string> Outages => ["unreachable", "timeout", "refused"];

    [Theory]
    [Trait("Rule", "AUTH-29")]
    [MemberData(nameof(Outages))]
    public async Task AUTH29_github_cannot_be_reached_or_refuses_the_code(string outage)
    {
        var (app, github) = await StartAsync();
        await using var _app = app;
        switch (outage)
        {
            case "unreachable": github.Down = new HttpRequestException("cut"); break;
            case "timeout": github.Down = new TaskCanceledException("timeout"); break;
            default: github.Token = """{"error":"bad_verification_code"}"""; break;
        }

        var response = await LoginAsync(Browser(app));

        Assert.Equal("/?login_error=github_unavailable", Location(response));
        Assert.Null(SetCookie(response, SessionCookie));
    }

    [Fact]
    [Trait("Rule", "AUTH-29")]
    public async Task AUTH29_a_call_with_no_answer_is_sent_again()
    {
        var (app, github) = await StartAsync();
        await using var _app = app;
        github.StallCalls = 2;

        var response = await LoginAsync(Browser(app));

        Assert.Equal("/", Location(response));
        Assert.NotNull(SetCookie(response, SessionCookie));
        Assert.Equal(3, github.Calls.Count(c => c.EndsWith("/login/oauth/access_token", StringComparison.Ordinal)));
    }

    [Fact]
    [Trait("Rule", "AUTH-29")]
    public async Task AUTH29_gives_up_after_the_tries_and_an_answer_is_not_sent_again()
    {
        var (app, github) = await StartAsync();
        await using var _app = app;
        github.StallCalls = 3;

        var stalled = await LoginAsync(Browser(app));
        github.Calls.Clear();
        github.Token = """{"error":"bad_verification_code"}""";
        var refused = await LoginAsync(Browser(app));

        Assert.Equal("/?login_error=github_unavailable", Location(stalled));
        Assert.Equal("/?login_error=github_unavailable", Location(refused));
        Assert.Single(github.Calls, c => c.EndsWith("/login/oauth/access_token", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Rule", "AUTH-29")]
    public async Task AUTH29_suspended_user_gets_no_session()
    {
        var (app, _) = await StartAsync();
        await using var _app = app;
        await PostgresServer.ScalarAsync<int>(app.ConnectionString,
            "INSERT INTO identity.users (email, github_id, display_name, created_at, suspended_at) VALUES ('sara@example.com', 42, 'Sara', now(), now()) RETURNING 1");

        var response = await LoginAsync(Browser(app));

        Assert.Equal("/?login_error=suspended", Location(response));
        Assert.Null(SetCookie(response, SessionCookie));
        Assert.Empty(await app.AccessLogAsync());
    }

    [Fact]
    [Trait("Rule", "AUTH-29")]
    public async Task AUTH29_every_way_back_deletes_the_state_cookie()
    {
        var (app, _) = await StartAsync();
        await using var _app = app;
        var browser = Browser(app);

        var response = await ComeBackAsync(browser, await StartLoginAsync(browser), "error=access_denied");

        Assert.Contains("expires=Thu, 01 Jan 1970", SetCookie(response, StateCookie), StringComparison.OrdinalIgnoreCase);
    }

    // ---- AUTH-30 ----

    /// <summary>A browser logged in by the development login (a phone user), that does not follow redirects.</summary>
    private static async Task<HttpClient> PhoneUserAsync(DalanApp app)
    {
        var browser = Browser(app);
        (await browser.PostAsJsonAsync("/api/dev/login", new { phone = "09121234567", name = "Sara" }, Cancel)).EnsureSuccessStatusCode();
        return browser;
    }

    private static async Task<HttpResponseMessage> LinkAsync(HttpClient browser)
    {
        var start = await browser.GetAsync("/api/account/github", Cancel);
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        return await ComeBackAsync(browser, QueryHelpers.ParseQuery(start.Headers.Location!.Query)["state"].ToString());
    }

    [Fact]
    [Trait("Rule", "AUTH-30")]
    public async Task AUTH30_link_a_free_github_account()
    {
        var (app, _) = await StartAsync();
        await using var _app = app;
        var browser = await PhoneUserAsync(app);
        var me = (await JsonAsync(await browser.GetAsync("/api/me", Cancel))).GetProperty("id").GetInt64();

        var response = await LinkAsync(browser);

        Assert.Equal("/account", Location(response));
        var profile = await JsonAsync(await browser.GetAsync("/api/me", Cancel));
        Assert.Equal(me, profile.GetProperty("id").GetInt64());
        Assert.True(profile.GetProperty("has_github").GetBoolean());
        Assert.Equal(1L, await CountUsersAsync(app));
    }

    [Fact]
    [Trait("Rule", "AUTH-30")]
    public async Task AUTH30_github_of_another_account_starts_a_merge()
    {
        var (app, _) = await StartAsync();
        await using var _app = app;
        await PostgresServer.ScalarAsync<int>(app.ConnectionString,
            "INSERT INTO identity.users (email, github_id, display_name, created_at) VALUES ('sara@example.com', 42, 'Sara', now()) RETURNING 1");
        var browser = await PhoneUserAsync(app);

        var response = await LinkAsync(browser);

        Assert.Equal("/account?merge=1", Location(response));
        var plan = await JsonAsync(await browser.GetAsync("/api/account/merge", Cancel));
        Assert.Equal("github", plan.GetProperty("way").GetString());
        Assert.Equal(2L, await CountUsersAsync(app));
    }
}
