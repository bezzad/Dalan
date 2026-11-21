using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Dalan.Testing.PostgresServer))]

namespace Dalan.Testing;

/// <summary>
/// One real PostgreSQL server for all tests of an assembly (docs/TESTING.md section 3).
/// It starts a container, or uses the server named in DALAN_TEST_POSTGRES when Docker is not there.
/// </summary>
public sealed class PostgresServer : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string _server = "";

    public async ValueTask InitializeAsync()
    {
        _server = Environment.GetEnvironmentVariable("DALAN_TEST_POSTGRES") ?? "";
        if (_server.Length > 0) return;

        // Test data is thrown away: no waiting for the disk. On a busy disk the flushes made every test slow
        // and the container hard to stop (a process waiting on the disk cannot be killed).
        _container = new PostgreSqlBuilder("postgres:18-alpine")
            .WithCommand("-c", "fsync=off", "-c", "synchronous_commit=off", "-c", "full_page_writes=off")
            .Build();
        await _container.StartAsync();
        _server = _container.GetConnectionString();
    }

    /// <summary>Creates an empty database and returns its connection string. Every test gets its own.</summary>
    public async Task<string> NewDatabaseAsync()
    {
        var name = $"dalan_test_{Guid.NewGuid():N}";
        await ExecuteAsync(_server, $"CREATE DATABASE {name}");

        // No pooling: hundreds of short-lived test databases must not keep idle connections open.
        return new NpgsqlConnectionStringBuilder(_server) { Database = name, Pooling = false, IncludeErrorDetail = true }
            .ConnectionString;
    }

    public static async Task<T> ScalarAsync<T>(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    public static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }
}
