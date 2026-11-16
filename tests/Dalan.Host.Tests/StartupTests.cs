using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Testing;

namespace Dalan.Host.Tests;

public class StartupTests(PostgresServer postgres)
{
    /// <summary>Nothing listens on port 1, so the database is "down".</summary>
    private const string NoDatabase = "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=2";

    [Fact]
    [Trait("Rule", "OPS-02")]
    public async Task OPS02_without_database_ready_is_503_and_health_is_200()
    {
        await using var app = new DalanApp(NoDatabase);
        var client = app.CreateClient();

        var ready = await client.GetAsync("/ready", TestContext.Current.CancellationToken);
        var health = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    [Trait("Rule", "OPS-08")]
    public async Task OPS08_calls_before_ready_get_503_not_ready()
    {
        await using var app = new DalanApp(NoDatabase);

        var response = await app.CreateClient().GetAsync("/v1/updates", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("not_ready", body.GetProperty("error").GetString());
    }

    [Fact]
    [Trait("Rule", "OPS-07")]
    public async Task OPS07_empty_database_gets_both_schemas_with_their_own_history()
    {
        await using var app = new DalanApp(await postgres.NewDatabaseAsync());
        await app.ReadyAsync();

        foreach (var table in new[] { "identity.__ef_migrations_history", "relay.__ef_migrations_history" })
        {
            Assert.True(
                await PostgresServer.ScalarAsync<bool>(app.ConnectionString, $"SELECT to_regclass('{table}') IS NOT NULL"),
                $"{table} is missing");
        }
    }

    [Fact]
    [Trait("Rule", "OPS-07")]
    public async Task OPS07_second_start_keeps_the_data()
    {
        var database = await postgres.NewDatabaseAsync();
        await using (var first = new DalanApp(database))
        {
            await first.ReadyAsync();
            await first.AddUserAsync();
        }

        await using var second = new DalanApp(database);
        await second.ReadyAsync();

        Assert.Equal(1L, await PostgresServer.ScalarAsync<long>(database, "SELECT count(*) FROM identity.users"));
    }
}
