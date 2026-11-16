using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Dalan.Host.Tests;

public class ProbeTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Health(string Status, string Version);

    [Fact]
    [Trait("Rule", "OPS-01")]
    public async Task OPS01_health_returns_ok_and_version()
    {
        var response = await factory.CreateClient().GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Health>(TestContext.Current.CancellationToken);
        Assert.Equal(new Health("ok", "dev"), body);
    }

    [Fact]
    [Trait("Rule", "OPS-02")]
    public async Task OPS02_ready_returns_200_when_started()
    {
        var response = await factory.CreateClient().GetAsync("/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
