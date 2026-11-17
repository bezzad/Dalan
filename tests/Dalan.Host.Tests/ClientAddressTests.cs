using System.Net;
using Dalan.Testing;

namespace Dalan.Host.Tests;

/// <summary>
/// Where the client address comes from. The address a call was given is read back from the access log (ADM-01):
/// a logged-in user calls the Client API and the row shows what the server took as the address.
/// </summary>
public class ClientAddressTests(PostgresServer postgres)
{
    private const string Proxy = "127.0.0.1:40000";

    [Fact]
    [Trait("Rule", "SEC-10")]
    public async Task SEC10_trusted_proxy_reports_address_and_port()
    {
        await using var app = await StartAsync();

        Assert.Equal("203.0.113.7 51000", await SeenAsAsync(app, socket: Proxy, forwardedFor: "203.0.113.7:51000"));
    }

    [Fact]
    [Trait("Rule", "SEC-10")]
    public async Task SEC10_header_from_a_caller_that_is_not_a_proxy_is_ignored()
    {
        await using var app = await StartAsync();

        Assert.Equal("198.51.100.50 4000", await SeenAsAsync(app, socket: "198.51.100.50:4000", forwardedFor: "203.0.113.7"));
    }

    [Fact]
    [Trait("Rule", "SEC-10")]
    public async Task SEC10_without_a_header_the_address_is_the_connection()
    {
        await using var app = await StartAsync();

        Assert.Equal("198.51.100.50 4000", await SeenAsAsync(app, socket: "198.51.100.50:4000", forwardedFor: null));
    }

    [Fact]
    [Trait("Rule", "SEC-10")]
    public async Task SEC10_behind_a_load_balancer_what_the_client_wrote_is_not_believed()
    {
        await using var app = await StartAsync(("Dalan:TrustedProxies", "127.0.0.1,10.0.0.5"));

        Assert.Equal("198.51.100.9", await SeenAsAsync(app, socket: "10.0.0.5:4000", forwardedFor: "1.1.1.1, 198.51.100.9"));
    }

    [Fact]
    [Trait("Rule", "SEC-10")]
    public async Task SEC10_two_trusted_proxies_in_a_row_give_the_client()
    {
        await using var app = await StartAsync(("Dalan:TrustedProxies", "10.0.0.0/24"));

        Assert.Equal("198.51.100.9", await SeenAsAsync(app, socket: "10.0.0.6:4000", forwardedFor: "198.51.100.9, 10.0.0.5"));
    }

    [Fact]
    [Trait("Rule", "SEC-10")]
    public async Task SEC10_a_wrong_entry_in_the_setting_stops_the_start()
    {
        await using var app = new DalanApp(await postgres.NewDatabaseAsync(), new Dictionary<string, string> { ["Dalan:TrustedProxies"] = "not-an-address" });

        Assert.Throws<FormatException>(() => app.CreateClient());
    }

    [Fact]
    [Trait("Rule", "SEC-02")]
    public async Task SEC02_a_faked_header_does_not_open_a_new_limit()
    {
        await using var app = await StartAsync(("Dalan:IpRequestLimit", "2"), ("Dalan:IpLimitWindowSeconds", "3600"));
        var caller = DalanApp.From(app.Browser(), "198.51.100.50:4000");

        var codes = new List<HttpStatusCode>();
        foreach (var faked in new[] { "1.1.1.1", "2.2.2.2", "3.3.3.3" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
            request.Headers.Add("X-Forwarded-For", faked);
            codes.Add((await caller.SendAsync(request, TestContext.Current.CancellationToken)).StatusCode);
        }

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests], codes);
    }

    [Fact]
    [Trait("Rule", "SEC-11")]
    public async Task SEC11_a_proxy_that_is_not_listed_is_warned_about_once()
    {
        await using var app = await StartAsync();

        await SeenAsAsync(app, socket: "10.0.0.5:4000", forwardedFor: "203.0.113.7");
        await SeenAsAsync(app, socket: "10.0.0.5:4001", forwardedFor: "203.0.113.8");

        var warning = Assert.Single(ProxyWarnings(app));
        Assert.Contains("10.0.0.5", warning, StringComparison.Ordinal);
        Assert.Contains("Dalan:TrustedProxies", warning, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Rule", "SEC-11")]
    public async Task SEC11_no_warning_for_a_public_caller_or_a_listed_proxy()
    {
        await using var app = await StartAsync();

        await SeenAsAsync(app, socket: "198.51.100.9:4000", forwardedFor: "203.0.113.7");
        await SeenAsAsync(app, socket: Proxy, forwardedFor: "203.0.113.8");

        Assert.Empty(ProxyWarnings(app));
    }

    private async Task<DalanApp> StartAsync(params (string Key, string Value)[] settings)
    {
        var app = new DalanApp(await postgres.NewDatabaseAsync(), settings.ToDictionary(s => s.Key, s => s.Value));
        await app.ReadyAsync();
        return app;
    }

    /// <summary>What the server logged about a proxy that is not listed.</summary>
    private static IEnumerable<string> ProxyWarnings(DalanApp app) =>
        app.Logs.Lines.Select(line => line.Text).Where(text => text.Contains("X-Forwarded-For", StringComparison.Ordinal));

    /// <summary>A new user makes one Client API call over this connection. Returns "address port" from the user's access log row.</summary>
    private static async Task<string> SeenAsAsync(DalanApp app, string socket, string? forwardedFor)
    {
        var (browser, userId) = await app.LoginAsync();
        DalanApp.From(browser, socket);
        if (forwardedFor is not null) browser.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);

        var response = await browser.GetAsync("/api/bots", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var prefix = $"User {userId} ";
        return Assert.Single(await app.AccessLogAsync(), row => row.StartsWith(prefix, StringComparison.Ordinal))[prefix.Length..];
    }
}
