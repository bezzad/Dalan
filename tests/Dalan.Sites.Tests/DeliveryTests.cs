using System.Net;
using Dalan.Testing;
using static Dalan.Sites.Tests.SitesApp;

namespace Dalan.Sites.Tests;

/// <summary>The one-line install (AGT-10) and the certificate question of the front proxy (SITE-17).</summary>
public class DeliveryTests(PostgresServer postgres)
{
    [Theory]
    [Trait("Rule", "AGT-10")]
    [InlineData("/install.sh", "/downloads/$file")]
    [InlineData("/install.ps1", "/downloads/dalan-windows-x64.exe")]
    public async Task AGT10_install_script_downloads_from_this_server(string script, string download)
    {
        await using var app = await StartAsync(postgres, ("Admin:SiteUrl", "https://dalan.test/"));

        var response = await app.CreateClient().GetAsync(script, Cancel);
        var text = await response.Content.ReadAsStringAsync(Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"https://dalan.test{download}", text, StringComparison.Ordinal);
        Assert.DoesNotContain("__DALAN_URL__", text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "AGT-10")]
    public async Task AGT10_agent_file_comes_from_the_agent_folder()
    {
        var folder = Directory.CreateTempSubdirectory("dalan-agent").FullName;
        await File.WriteAllTextAsync(Path.Combine(folder, "dalan-linux-x64"), "native agent", Cancel);
        await File.WriteAllTextAsync(Path.Combine(folder, "secret.txt"), "not for download", Cancel);
        await using var app = await StartAsync(postgres, ("Sites:AgentFolder", folder));
        var client = app.CreateClient();

        var file = await client.GetAsync("/downloads/dalan-linux-x64", Cancel);
        var other = await client.GetAsync("/downloads/secret.txt", Cancel);
        var missing = await client.GetAsync("/downloads/dalan-macos-arm64", Cancel);

        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal("native agent", await file.Content.ReadAsStringAsync(Cancel));
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    [Trait("Rule", "SITE-17")]
    public async Task SITE17_certificate_only_for_a_claimed_site_and_only_for_the_server_itself()
    {
        await using var app = await StartAsync(postgres);
        var (_, _, key) = await app.BuilderAsync();
        await app.Agent(key).SessionAsync("mysite");
        var server = DalanApp.From(app.CreateClient(), "127.0.0.1:40000");
        var outside = DalanApp.From(app.CreateClient(), "5.6.7.8:1234");

        Assert.Equal(HttpStatusCode.OK, (await server.GetAsync($"/api/sites/tls?domain=mysite.{Domain}", Cancel)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await server.GetAsync($"/api/sites/tls?domain=nobody.{Domain}", Cancel)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await server.GetAsync($"/api/sites/tls?domain=a.mysite.{Domain}", Cancel)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await server.GetAsync("/api/sites/tls?domain=mysite.example.com", Cancel)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outside.GetAsync($"/api/sites/tls?domain=mysite.{Domain}", Cancel)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "SITE-17")]
    public async Task SITE17_certificate_for_the_demo_host_only_when_demos_are_on()
    {
        await using var on = await StartAsync(postgres, ("Sites:DemoHost", "demo.test"));
        await using var off = await StartAsync(postgres, ("Sites:DemoHost", ""));
        var onServer = DalanApp.From(on.CreateClient(), "127.0.0.1:40000");
        var offServer = DalanApp.From(off.CreateClient(), "127.0.0.1:40000");
        var outside = DalanApp.From(on.CreateClient(), "5.6.7.8:1234");

        Assert.Equal(HttpStatusCode.OK, (await onServer.GetAsync("/api/sites/tls?domain=demo.test", Cancel)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await onServer.GetAsync("/api/sites/tls?domain=other.test", Cancel)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await offServer.GetAsync("/api/sites/tls?domain=demo.test", Cancel)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outside.GetAsync("/api/sites/tls?domain=demo.test", Cancel)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "SEC-16")]
    [Trait("Rule", "SITE-17")]
    public async Task SEC16_certificate_for_the_view_host_only_when_the_viewer_is_on()
    {
        await using var on = await StartAsync(postgres);
        await using var set = await StartAsync(postgres, ("Relay:FileViewHost", "files.test"));
        await using var off = await StartAsync(postgres, ("Sites:Domain", ""), ("Relay:FileViewHost", ""));
        var onServer = DalanApp.From(on.CreateClient(), "127.0.0.1:40000");
        var setServer = DalanApp.From(set.CreateClient(), "127.0.0.1:40000");
        var offServer = DalanApp.From(off.CreateClient(), "127.0.0.1:40000");
        var outside = DalanApp.From(on.CreateClient(), "5.6.7.8:1234");

        Assert.Equal(HttpStatusCode.OK, (await onServer.GetAsync($"/api/sites/tls?domain=view.{Domain}", Cancel)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await setServer.GetAsync("/api/sites/tls?domain=files.test", Cancel)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await setServer.GetAsync($"/api/sites/tls?domain=view.{Domain}", Cancel)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await offServer.GetAsync("/api/sites/tls?domain=view.", Cancel)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outside.GetAsync($"/api/sites/tls?domain=view.{Domain}", Cancel)).StatusCode);
    }
}
