using System.Net;
using System.Text.RegularExpressions;
using Dalan.Relay.Bots;
using Dalan.Testing;
using Microsoft.EntityFrameworkCore;

namespace Dalan.Relay.Tests;

public class BotTests(PostgresServer postgres)
{
    [Fact]
    [Trait("Rule", "REL-01")]
    public async Task REL01_relay_tables_are_in_the_relay_schema_with_jsonb_content()
    {
        await using var app = await RelayApp.StartAsync(postgres);

        foreach (var table in new[] { "relay.bots", "relay.memberships", "relay.messages" })
        {
            Assert.True(
                await PostgresServer.ScalarAsync<bool>(app.ConnectionString, $"SELECT to_regclass('{table}') IS NOT NULL"),
                $"{table} is missing");
        }

        Assert.Equal("jsonb", await PostgresServer.ScalarAsync<string>(app.ConnectionString, """
            SELECT data_type FROM information_schema.columns
            WHERE table_schema = 'relay' AND table_name = 'messages' AND column_name = 'content'
            """));
    }

    [Fact]
    [Trait("Rule", "REL-01")]
    public async Task REL01_no_foreign_key_crosses_a_schema()
    {
        await using var app = await RelayApp.StartAsync(postgres);

        var crossing = await PostgresServer.ScalarAsync<long>(app.ConnectionString, """
            SELECT count(*) FROM pg_constraint c
            JOIN pg_class child ON child.oid = c.conrelid
            JOIN pg_class parent ON parent.oid = c.confrelid
            WHERE c.contype = 'f' AND child.relnamespace <> parent.relnamespace
            """);

        Assert.Equal(0, crossing);
    }

    [Fact]
    [Trait("Rule", "REL-02")]
    public async Task REL02_bot_username_is_unique_without_regard_to_case()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        await app.AddBotAsync("ArcaneBot");

        await Assert.ThrowsAsync<DbUpdateException>(() => app.AddBotAsync("arcanebot"));

        Assert.Equal(1L, await PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT count(*) FROM relay.bots"));
    }

    [Fact]
    [Trait("Rule", "REL-03")]
    public async Task REL03_token_is_bot_id_and_35_random_characters()
    {
        await using var app = await RelayApp.StartAsync(postgres);

        var (firstId, firstToken) = await app.AddBotAsync("first");
        var (secondId, secondToken) = await app.AddBotAsync("second");

        Assert.Matches($"^{firstId}:[A-Za-z0-9_-]{{35}}$", firstToken);
        Assert.Matches($"^{secondId}:[A-Za-z0-9_-]{{35}}$", secondToken);
        Assert.NotEqual(firstToken.Split(':')[1], secondToken.Split(':')[1]);
    }

    [Fact]
    [Trait("Rule", "REL-03")]
    public async Task REL03_database_row_does_not_contain_the_secret()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (_, token) = await app.AddBotAsync();
        var secret = token.Split(':')[1];

        var row = await PostgresServer.ScalarAsync<string>(app.ConnectionString, "SELECT b::text FROM relay.bots b");

        Assert.DoesNotContain(secret, row, StringComparison.Ordinal);
        var hash = Convert.ToHexString(BotToken.Hash(secret));
        Assert.Contains(hash, row, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Rule", "REL-04")]
    public async Task REL04_call_without_token_is_401()
    {
        await using var app = await RelayApp.StartAsync(postgres);

        var response = await app.BotClient(token: null).GetAsync("/v1/updates?timeout=0", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthorized", await response.ErrorCodeAsync());
    }

    [Theory]
    [Trait("Rule", "REL-04")]
    [InlineData("{id}:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("999999:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("{id}")]
    [InlineData("not-a-token")]
    public async Task REL04_wrong_or_malformed_token_is_401(string tokenPattern)
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, _) = await app.AddBotAsync();
        var token = tokenPattern.Replace("{id}", botId.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

        var response = await app.BotClient(token).GetAsync("/v1/updates?timeout=0", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthorized", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "REL-04")]
    public async Task REL04_valid_token_is_served_as_that_bot()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, token) = await app.AddBotAsync("mine");
        var (otherBotId, _) = await app.AddBotAsync("other");
        var user = await app.AddUserAsync();
        await app.UserSaysAsync(otherBotId, user, "for the other bot");
        await app.UserSaysAsync(botId, user, "for me");

        var updates = await app.BotClient(token).UpdatesAsync();

        Assert.Equal("for me", Assert.Single(updates).Text());
    }

    [Fact]
    [Trait("Rule", "REL-03")]
    public void REL03_token_parser_reads_id_and_secret()
    {
        Assert.True(BotToken.TryParse("42:abc", out var botId, out var secret));
        Assert.Equal(42, botId);
        Assert.Equal("abc", secret);
        Assert.False(BotToken.TryParse(":abc", out _, out _));
        Assert.False(BotToken.TryParse("42:", out _, out _));
        Assert.False(BotToken.TryParse("-1:abc", out _, out _));
        Assert.False(BotToken.TryParse(null, out _, out _));
    }
}
