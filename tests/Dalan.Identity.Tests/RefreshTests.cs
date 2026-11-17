using System.Net;
using System.Net.Http.Json;
using Dalan.Testing;
using static Dalan.Identity.Tests.Http;

namespace Dalan.Identity.Tests;

public class RefreshTests(PostgresServer postgres)
{
    private const string Count = "SELECT count(*) FROM identity.sessions";

    [Fact]
    [Trait("Rule", "AUTH-08")]
    public async Task AUTH08_login_sets_a_refresh_cookie_that_scripts_and_other_sites_cannot_use()
    {
        await using var app = await StartAsync(postgres, ("Identity:RefreshTokenDays", "30"));

        var response = await NoJar(app).PostAsJsonAsync("/api/dev/login", new { phone = "09121234567", name = "Sara" }, Cancel);

        var setCookie = SetCookie(response, RefreshCookie)!;
        var flags = setCookie.Split(';').Skip(1).Select(flag => flag.Trim().ToLowerInvariant()).ToList();
        Assert.Contains("httponly", flags);
        Assert.Contains("secure", flags);
        Assert.Contains("samesite=strict", flags);
        Assert.Contains("path=/", flags);
        Assert.Contains($"max-age={30 * 24 * 3600}", flags);
        Assert.DoesNotContain(flags, flag => flag.StartsWith("domain", StringComparison.Ordinal));
        // 256 bits as base64url is 43 characters.
        Assert.True(CookieValue(response, RefreshCookie).Length >= 43);
    }

    [Fact]
    [Trait("Rule", "AUTH-08")]
    public async Task AUTH08_only_a_hash_of_the_token_is_stored()
    {
        await using var app = await StartAsync(postgres);

        var (_, refresh) = await LoginRawAsync(app);

        Assert.Equal(1L, await PostgresServer.ScalarAsync<long>(app.ConnectionString, Count));
        Assert.Equal(Sha256Hex(refresh), await PostgresServer.ScalarAsync<string>(
            app.ConnectionString, "SELECT encode(token_hash, 'hex') FROM identity.sessions"));
        Assert.Equal(0L, await PostgresServer.ScalarAsync<long>(
            app.ConnectionString, $"SELECT count(*) FROM identity.sessions s WHERE s::text LIKE '%{refresh}%'"));
    }

    [Fact]
    [Trait("Rule", "AUTH-09")]
    public async Task AUTH09_session_goes_on_after_the_access_token_ends()
    {
        await using var app = await StartAsync(postgres);
        var (session, refresh) = await LoginRawAsync(app);
        app.Time.Advance(TimeSpan.FromMinutes(16));
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(app, HttpMethod.Get, "/api/me", session)).StatusCode);

        var refreshed = await RefreshAsync(app, refresh);

        Assert.Equal(HttpStatusCode.NoContent, refreshed.StatusCode);
        var me = await SendAsync(app, HttpMethod.Get, "/api/me", CookieValue(refreshed, SessionCookie));
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(
            await PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT id FROM identity.users"),
            (await JsonAsync(me)).GetProperty("id").GetInt64());
    }

    [Fact]
    [Trait("Rule", "AUTH-09")]
    public async Task AUTH09_refresh_replaces_the_token()
    {
        await using var app = await StartAsync(postgres);
        var (_, refresh) = await LoginRawAsync(app);

        var next = CookieValue(await RefreshAsync(app, refresh), RefreshCookie);

        Assert.NotEqual(refresh, next);
        Assert.Equal(1L, await PostgresServer.ScalarAsync<long>(app.ConnectionString, Count));
        Assert.Equal(Sha256Hex(next), await PostgresServer.ScalarAsync<string>(
            app.ConnectionString, "SELECT encode(token_hash, 'hex') FROM identity.sessions"));
    }

    [Theory]
    [Trait("Rule", "AUTH-09")]
    [InlineData(null)]
    [InlineData("a-token-we-never-made")]
    public async Task AUTH09_missing_or_unknown_token_is_401(string? refresh)
    {
        await using var app = await StartAsync(postgres);
        await LoginRawAsync(app);

        var response = await RefreshAsync(app, refresh);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthorized", await ErrorCodeAsync(response));
        Assert.Null(SetCookie(response, SessionCookie));
    }

    [Fact]
    [Trait("Rule", "AUTH-09")]
    public async Task AUTH09_token_ends_after_its_lifetime()
    {
        await using var app = await StartAsync(postgres, ("Identity:RefreshTokenDays", "30"));
        var (_, refresh) = await LoginRawAsync(app);

        app.Time.Advance(TimeSpan.FromDays(30));

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(app, refresh)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-09")]
    public async Task AUTH09_each_refresh_adds_life()
    {
        await using var app = await StartAsync(postgres, ("Identity:RefreshTokenDays", "30"));
        var (_, refresh) = await LoginRawAsync(app);
        app.Time.Advance(TimeSpan.FromDays(20));
        var next = CookieValue(await RefreshAsync(app, refresh), RefreshCookie);

        app.Time.Advance(TimeSpan.FromDays(25));

        Assert.Equal(HttpStatusCode.NoContent, (await RefreshAsync(app, next)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-09")]
    public async Task AUTH09_token_of_a_deleted_user_is_401()
    {
        await using var app = await StartAsync(postgres);
        var (_, refresh) = await LoginRawAsync(app);

        await PostgresServer.ExecuteAsync(app.ConnectionString, "DELETE FROM identity.users");

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(app, refresh)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-10")]
    public async Task AUTH10_old_token_used_later_ends_the_session()
    {
        await using var app = await StartAsync(postgres, ("Identity:RefreshReuseGraceSeconds", "10"));
        var (_, a) = await LoginRawAsync(app);
        var b = CookieValue(await RefreshAsync(app, a), RefreshCookie);

        app.Time.Advance(TimeSpan.FromSeconds(11));
        var reuse = await RefreshAsync(app, a);

        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(app, b)).StatusCode);
        Assert.Equal(0L, await PostgresServer.ScalarAsync<long>(app.ConnectionString, Count));
    }

    [Fact]
    [Trait("Rule", "AUTH-10")]
    public async Task AUTH10_two_tabs_at_the_same_moment_do_not_end_the_session()
    {
        await using var app = await StartAsync(postgres, ("Identity:RefreshReuseGraceSeconds", "10"));
        var (_, a) = await LoginRawAsync(app);
        var b = CookieValue(await RefreshAsync(app, a), RefreshCookie);

        app.Time.Advance(TimeSpan.FromSeconds(2));
        var secondTab = await RefreshAsync(app, a);

        Assert.Equal(HttpStatusCode.Unauthorized, secondTab.StatusCode);
        // The second tab must keep the cookies the first tab just got.
        Assert.Null(SetCookie(secondTab, RefreshCookie));
        Assert.Null(SetCookie(secondTab, SessionCookie));
        Assert.Equal(HttpStatusCode.NoContent, (await RefreshAsync(app, b)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-10")]
    public async Task AUTH10_reuse_on_one_device_leaves_the_other_device_alone()
    {
        await using var app = await StartAsync(postgres);
        var (_, phoneA) = await LoginRawAsync(app);
        var (_, laptop) = await LoginRawAsync(app);
        await RefreshAsync(app, phoneA);

        app.Time.Advance(TimeSpan.FromMinutes(1));
        await RefreshAsync(app, phoneA);

        Assert.Equal(HttpStatusCode.NoContent, (await RefreshAsync(app, laptop)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-05")]
    public async Task AUTH05_logout_removes_both_cookies_and_kills_the_refresh_token()
    {
        await using var app = await StartAsync(postgres);
        var (session, refresh) = await LoginRawAsync(app);

        var logout = await SendAsync(app, HttpMethod.Post, "/api/logout", session, refresh);

        // An empty value with a date in the past: the browser drops the cookie.
        Assert.StartsWith($"{SessionCookie}=; expires=Thu, 01 Jan 1970", SetCookie(logout, SessionCookie), StringComparison.Ordinal);
        Assert.StartsWith($"{RefreshCookie}=; expires=Thu, 01 Jan 1970", SetCookie(logout, RefreshCookie), StringComparison.Ordinal);
        var later = await RefreshAsync(app, refresh);
        Assert.Equal(HttpStatusCode.Unauthorized, later.StatusCode);
        Assert.Equal("unauthorized", await ErrorCodeAsync(later));
    }

    [Fact]
    [Trait("Rule", "AUTH-05")]
    public async Task AUTH05_logout_on_one_device_leaves_the_other_logged_in()
    {
        await using var app = await StartAsync(postgres);
        var (session, phone) = await LoginRawAsync(app);
        var (_, laptop) = await LoginRawAsync(app);

        await SendAsync(app, HttpMethod.Post, "/api/logout", session, phone);

        Assert.Equal(HttpStatusCode.NoContent, (await RefreshAsync(app, laptop)).StatusCode);
    }
}
