using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Testing;

namespace Dalan.Host.Tests;

public class ClientAppTests(PostgresServer postgres)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private async Task<DalanApp> StartAsync(params (string Key, string Value)[] settings)
    {
        var app = new DalanApp(await postgres.NewDatabaseAsync(), settings.ToDictionary(s => s.Key, s => s.Value));
        await app.ReadyAsync();
        return app;
    }

    [Theory]
    [Trait("Rule", "CLI-56")]
    [Trait("Rule", "CLI-57")]
    [InlineData("/about")]
    [InlineData("/contact")]
    public async Task CLI56_about_and_contact_pages_are_served(string path)
    {
        await using var app = await StartAsync();

        var response = await app.CreateClient().GetAsync(path, Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [Trait("Rule", "CLI-01")]
    [Trait("Rule", "CLI-77")]
    [Trait("Rule", "CLI-81")]
    [InlineData("/")]
    [InlineData("/bots")]
    [InlineData("/apps")]
    [InlineData("/@ArcaneBot")]
    [InlineData("/privacy")]
    [InlineData("/faq")]
    [InlineData("/showcase")]
    [InlineData("/guide")]
    [InlineData("/guide/bot")]
    [InlineData("/guide/site")]
    [InlineData("/chats")]
    [InlineData("/bots/new")]
    [InlineData("/bots/mofidbot")]
    [InlineData("/account")]
    [InlineData("/account/profile")]
    [InlineData("/account/security")]
    [InlineData("/account/notifications")]
    [InlineData("/account/preferences")]
    [InlineData("/account/builder")]
    [InlineData("/account/keys")]
    [InlineData("/admin")]
    // CLI-81: the dashboard and every address under it; the old addresses above move into it in the page.
    [InlineData("/dashboard")]
    [InlineData("/dashboard/chats")]
    [InlineData("/dashboard/bots/mofidbot")]
    [InlineData("/dashboard/admin/settings")]
    public async Task CLI01_page_is_served_and_not_cached(string path)
    {
        await using var app = await StartAsync();

        var response = await app.CreateClient().GetAsync(path, Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());
        var html = await response.Content.ReadAsStringAsync(Cancel);
        Assert.Contains("""<html lang="fa" dir="rtl">""", html, StringComparison.Ordinal);
        Assert.Contains("""src="/main.js""", html, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Rule", "CLI-01")]
    [InlineData("/main.js", "text/javascript")]
    [InlineData("/app.css", "text/css")]
    [InlineData("/fonts/vazirmatn.woff2", "font/woff2")]
    [InlineData("/icons/icon.svg", "image/svg+xml")]
    public async Task CLI01_client_files_come_from_the_same_server(string path, string contentType)
    {
        await using var app = await StartAsync();

        var response = await app.CreateClient().GetAsync(path, Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());
        Assert.NotNull(response.Headers.ETag);
    }

    [Theory]
    [Trait("Rule", "CLI-01")]
    [InlineData("/nothing-here")]
    [InlineData("/@bot/extra")]
    [InlineData("/api/nothing-here")]
    public async Task CLI01_unknown_path_is_404(string path)
    {
        await using var app = await StartAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await app.CreateClient().GetAsync(path, Cancel)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "CLI-32")]
    public async Task CLI32_the_site_serves_the_cli()
    {
        await using var app = await StartAsync();

        var response = await app.CreateClient().GetAsync("/dalan.mjs", Cancel);
        var text = await response.Content.ReadAsStringAsync(Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/javascript", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("#!/usr/bin/env node", text, StringComparison.Ordinal);
        Assert.Contains("DALAN_KEY", text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "CLI-09")]
    public async Task CLI09_manifest_makes_the_app_installable()
    {
        await using var app = await StartAsync();
        var client = app.CreateClient();
        Assert.Contains("""<link rel="manifest" href="/manifest.webmanifest">""", await client.GetStringAsync("/", Cancel), StringComparison.Ordinal);

        var response = await client.GetAsync("/manifest.webmanifest", Cancel);

        Assert.Equal("application/manifest+json", response.Content.Headers.ContentType?.MediaType);
        var manifest = await response.Content.ReadFromJsonAsync<JsonElement>(Cancel);
        Assert.False(string.IsNullOrWhiteSpace(manifest.GetProperty("name").GetString()));
        Assert.Equal("standalone", manifest.GetProperty("display").GetString());
        Assert.Equal("/", manifest.GetProperty("start_url").GetString());

        var icons = manifest.GetProperty("icons").EnumerateArray().Select(icon => icon.GetProperty("src").GetString()!).ToList();
        Assert.NotEmpty(icons);
        foreach (var icon in icons)
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(icon, Cancel)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "CLI-09")]
    public async Task CLI09_service_worker_is_served_and_not_cached()
    {
        await using var app = await StartAsync();

        var response = await app.CreateClient().GetAsync("/sw.js", Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/javascript", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());
        Assert.Contains("""register("/sw.js")""", await app.CreateClient().GetStringAsync("/main.js", Cancel), StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Rule", "SEC-04")]
    [InlineData("/")]
    [InlineData("/main.js")]
    [InlineData("/api/me")]
    [InlineData("/v1/updates")]
    [InlineData("/nothing-here")]
    public async Task SEC04_every_response_tells_the_browser_to_load_only_from_our_origin(string path)
    {
        // The file viewer off (SEC-16 adds only its frame-src when it is on).
        await using var app = await StartAsync(("Sites:Domain", ""), ("Relay:FileViewHost", ""));

        var response = await app.CreateClient().GetAsync(path, Cancel);

        Assert.Equal(
            "default-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'",
            Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("same-origin", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
    }

    [Theory]
    [Trait("Rule", "SEC-16")]
    [InlineData("/")]
    [InlineData("/api/me")]
    public async Task SEC16_app_may_frame_only_the_view_host(string path)
    {
        await using var app = await StartAsync(("Sites:Domain", "dalans.ir"), ("Sites:Scheme", "https"), ("Relay:FileViewHost", ""));

        var response = await app.CreateClient().GetAsync(path, Cancel);

        Assert.Equal(
            "default-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'; frame-src https://view.dalans.ir",
            Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
    }

    [Fact]
    [Trait("Rule", "SEC-16")]
    public async Task SEC16_view_host_goes_before_the_sites()
    {
        // "view" is under the sites domain, so the sites routing would take it as a site (SITE-08).
        await using var app = await StartAsync(("Sites:Domain", "dalans.ir"), ("Relay:FileViewHost", ""));
        var tab = app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://view.dalans.ir") });

        var response = await tab.GetAsync("/v/nothing", Cancel);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("", await response.Content.ReadAsStringAsync(Cancel));
    }
}
