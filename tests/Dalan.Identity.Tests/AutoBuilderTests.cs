using System.Net.Http.Json;
using Dalan.Identity.Data;
using Dalan.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using static Dalan.Identity.Tests.Http;

namespace Dalan.Identity.Tests;

/// <summary>AUTH-39: a verified phone makes a builder without the admin.</summary>
public class AutoBuilderTests(PostgresServer postgres)
{
    private const string Phone = "09121234567";

    private Task<DalanApp> StartAsync(bool on = true) => Http.StartAsync(postgres, ("Identity:AutoBuilder", on ? "true" : "false"));

    private static async Task<string?> BuilderAsync(HttpClient browser) =>
        (await JsonAsync(await browser.GetAsync("/api/me", Cancel))).GetProperty("builder").GetString();

    [Fact]
    [Trait("Rule", "AUTH-39")]
    public async Task AUTH39_a_new_sms_user_is_a_builder()
    {
        await using var app = await StartAsync();

        var browser = await app.LoginBySmsAsync(Phone);

        Assert.Equal("approved", await BuilderAsync(browser));
    }

    [Fact]
    [Trait("Rule", "AUTH-39")]
    public async Task AUTH39_an_added_phone_makes_a_builder()
    {
        await using var app = await StartAsync();
        var browser = await app.LoginByEmailAsync("sara@example.com");
        (await DalanApp.AskCodeAsync(browser, Phone)).EnsureSuccessStatusCode();

        (await browser.PostAsJsonAsync("/api/account/phone", new { phone = Phone, code = app.Sms.Sent[^1].Code }, Cancel))
            .EnsureSuccessStatusCode();

        Assert.Equal("approved", await BuilderAsync(browser));
    }

    [Fact]
    [Trait("Rule", "AUTH-39")]
    public async Task AUTH39_refused_stays_refused()
    {
        await using var app = await StartAsync();
        await app.AddUserAsync(phone: "+989121234567");
        await PostgresServer.ExecuteAsync(app.ConnectionString, "UPDATE identity.users SET builder = 3");

        var browser = await app.LoginBySmsAsync(Phone);

        Assert.Equal("refused", await BuilderAsync(browser));
    }

    [Fact]
    [Trait("Rule", "AUTH-39")]
    public async Task AUTH39_off_leaves_the_admin_to_decide()
    {
        await using var app = await StartAsync(on: false);

        var browser = await app.LoginBySmsAsync(Phone);

        Assert.Equal("none", await BuilderAsync(browser));
    }

    [Fact]
    [Trait("Rule", "AUTH-39")]
    public async Task AUTH39_an_open_request_is_approved_at_the_sms_login()
    {
        await using var app = await StartAsync();
        await app.AddUserAsync(phone: "+989121234567");
        await PostgresServer.ExecuteAsync(app.ConnectionString, "UPDATE identity.users SET builder = 1");

        var browser = await app.LoginBySmsAsync(Phone);

        Assert.Equal("approved", await BuilderAsync(browser));
    }

    [Fact]
    [Trait("Rule", "AUTH-39")]
    public async Task AUTH39_the_old_button_approves_a_request_and_leaves_refused()
    {
        await using var app = await StartAsync();
        var browser = await app.LoginBySmsAsync(Phone);
        await PostgresServer.ExecuteAsync(app.ConnectionString, "UPDATE identity.users SET builder = 1");
        (await browser.PostAsync("/api/account/builder", null, Cancel)).EnsureSuccessStatusCode();
        var afterRequest = await BuilderAsync(browser);
        await PostgresServer.ExecuteAsync(app.ConnectionString, "UPDATE identity.users SET builder = 3");

        (await browser.PostAsync("/api/account/builder", null, Cancel)).EnsureSuccessStatusCode();

        Assert.Equal("approved", afterRequest);
        Assert.Equal("refused", await BuilderAsync(browser));
    }

    [Fact]
    [Trait("Rule", "AUTH-39")]
    public async Task AUTH39_install_approves_every_phone_user_but_the_refused()
    {
        var database = await postgres.NewDatabaseAsync();
        var options = new DbContextOptionsBuilder<IdentityDb>()
            .UseNpgsql(database, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", IdentityDb.Schema))
            .UseSnakeCaseNamingConvention().ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning)).Options;
        await using (var db = new IdentityDb(options))
        {
            await db.GetService<IMigrator>().MigrateAsync("20261127200000_SessionListAndKeyAddresses", Cancel);
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO identity.users (phone, email, display_name, created_at, builder) VALUES
                    ('+989120000001', NULL, 'none', now(), 0),
                    ('+989120000002', NULL, 'requested', now(), 1),
                    ('+989120000003', NULL, 'approved', now(), 2),
                    ('+989120000004', NULL, 'refused', now(), 3),
                    (NULL, 'mail@example.com', 'no phone', now(), 0);
                """, Cancel);
        }

        await using (var db = new IdentityDb(options)) await db.Database.MigrateAsync(Cancel);

        await using var after = new IdentityDb(options);
        var states = await after.Users.AsNoTracking().OrderBy(u => u.Id).Select(u => (int)u.Builder).ToListAsync(Cancel);
        Assert.Equal([2, 2, 2, 3, 0], states);
    }
}
