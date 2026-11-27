using System.Net;
using System.Net.Http.Json;
using Dalan.Testing;

namespace Dalan.Host.Tests;

public class ProbeTests(PostgresServer postgres)
{
    private sealed record Health(string Status, string Version, string Commit);

    [Fact]
    [Trait("Rule", "OPS-01")]
    public async Task OPS01_health_returns_ok_and_version()
    {
        await using var app = new DalanApp(await postgres.NewDatabaseAsync());

        var response = await app.CreateClient().GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Health>(TestContext.Current.CancellationToken);
        Assert.Equal(new Health("ok", "dev", ""), body);
    }

    [Fact]
    [Trait("Rule", "OPS-17")]
    public async Task OPS17_health_gives_the_version_and_commit_from_the_build()
    {
        var settings = new Dictionary<string, string> { ["Dalan:Version"] = "v26.11.25.114726", ["Dalan:Commit"] = "7207d600e166" };
        await using var app = new DalanApp(await postgres.NewDatabaseAsync(), settings);

        var body = await app.CreateClient().GetFromJsonAsync<Health>("/health", TestContext.Current.CancellationToken);

        Assert.Equal(new Health("ok", "v26.11.25.114726", "7207d600e166"), body);
    }

    [Fact]
    [Trait("Rule", "OPS-17")]
    public async Task OPS17_a_local_run_has_version_dev_and_no_commit()
    {
        await using var app = new DalanApp(await postgres.NewDatabaseAsync());

        var body = await app.CreateClient().GetFromJsonAsync<Health>("/health", TestContext.Current.CancellationToken);

        Assert.Equal("dev", body?.Version);
        Assert.Equal("", body?.Commit);
    }

    [Fact]
    [Trait("Rule", "OPS-02")]
    public async Task OPS02_ready_returns_200_when_started()
    {
        await using var app = new DalanApp(await postgres.NewDatabaseAsync());
        await app.ReadyAsync();

        var response = await app.CreateClient().GetAsync("/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
