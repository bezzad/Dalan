using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Testing;
using static Dalan.Sites.Tests.SitesApp;

namespace Dalan.Sites.Tests;

/// <summary>SITE-25: every admin hears of each new site name, by a reserve or a first claim; never of a demo or a reconnect.</summary>
public class NewSiteNoticeTests(PostgresServer postgres)
{
    private const string SecondAdminPhone = "09129990002";

    private static async Task<JsonElement[]> NoticesAsync(HttpClient browser) =>
        [.. (await browser.GetFromJsonAsync<JsonElement>("/api/inbox", Cancel)).GetProperty("notices").EnumerateArray()];

    [Fact]
    [Trait("Rule", "SITE-25")]
    public async Task SITE25_a_reserve_tells_every_admin()
    {
        await using var app = await StartAsync(postgres, ("Identity:AdminPhones", $"{AdminPhone},{SecondAdminPhone}"));
        var (second, _) = await app.LoginAsync("Second", SecondAdminPhone);
        var (builder, userId, _) = await app.BuilderAsync("Ali");
        var admin = await app.AdminAsync();

        var reserve = await builder.PostAsJsonAsync("/api/sites", new { name = "shop" }, Cancel);

        Assert.Equal(HttpStatusCode.Created, reserve.StatusCode);
        foreach (var browser in new[] { admin, second })
        {
            var notice = Assert.Single(await NoticesAsync(browser));
            Assert.Contains($"shop.{Domain}", notice.GetProperty("title").GetString(), StringComparison.Ordinal);
            Assert.Contains("Ali", notice.GetProperty("text").GetString(), StringComparison.Ordinal);
            Assert.Contains(userId.ToString(System.Globalization.CultureInfo.InvariantCulture), notice.GetProperty("text").GetString(), StringComparison.Ordinal);
            Assert.Equal($"/dashboard/admin/users/{userId}", notice.GetProperty("link").GetString());
        }

        Assert.Empty(await NoticesAsync(builder));
    }

    [Fact]
    [Trait("Rule", "SITE-25")]
    public async Task SITE25_a_first_claim_by_the_agent_tells_the_admin_once()
    {
        await using var app = await StartAsync(postgres);
        var (_, userId, key) = await app.BuilderAsync("Ali");
        var admin = await app.AdminAsync();
        var agent = app.Agent(key);

        await agent.SessionAsync("shop", "blog");
        // Same names again: the agent connects again.
        await agent.SessionAsync("shop");
        (await app.Agent(key).OpenAsync("blog", "shop")).EnsureSuccessStatusCode();

        var titles = (await NoticesAsync(admin)).Select(n => n.GetProperty("title").GetString()).ToArray();
        Assert.Equal(2, titles.Length);
        Assert.Contains(titles, title => title!.Contains($"shop.{Domain}", StringComparison.Ordinal));
        Assert.Contains(titles, title => title!.Contains($"blog.{Domain}", StringComparison.Ordinal));
        Assert.All(await NoticesAsync(admin), notice => Assert.Equal($"/dashboard/admin/users/{userId}", notice.GetProperty("link").GetString()));
    }

    [Fact]
    [Trait("Rule", "SITE-25")]
    public async Task SITE25_reserving_an_own_name_again_adds_nothing()
    {
        await using var app = await StartAsync(postgres);
        var (builder, _, _) = await app.BuilderAsync("Ali");
        var admin = await app.AdminAsync();
        (await builder.PostAsJsonAsync("/api/sites", new { name = "shop" }, Cancel)).EnsureSuccessStatusCode();

        var again = await builder.PostAsJsonAsync("/api/sites", new { name = "shop" }, Cancel);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Single(await NoticesAsync(admin));
    }

    [Fact]
    [Trait("Rule", "SITE-25")]
    public async Task SITE25_a_demo_or_a_refused_name_adds_nothing()
    {
        await using var app = await StartAsync(postgres, ("Sites:DemoHost", "demo.test"));
        var (builder, _, key) = await app.BuilderAsync("Ali");
        var (other, _, _) = await app.BuilderAsync("Other");
        var admin = await app.AdminAsync();
        (await other.PostAsJsonAsync("/api/sites", new { name = "taken" }, Cancel)).EnsureSuccessStatusCode();
        var before = (await NoticesAsync(admin)).Length;

        var demo = await app.Agent(key).PostAsJsonAsync("/api/tunnel/sessions", new { demo = "new" }, Cancel);
        var taken = await builder.PostAsJsonAsync("/api/sites", new { name = "taken" }, Cancel);

        Assert.Equal(HttpStatusCode.OK, demo.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal(before, (await NoticesAsync(admin)).Length);
    }
}
