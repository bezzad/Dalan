using Dalan.Identity.Contracts;
using Dalan.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Identity.Tests;

public class UserTests(PostgresServer postgres)
{
    private async Task<DalanApp> StartAsync()
    {
        var app = new DalanApp(await postgres.NewDatabaseAsync());
        await app.ReadyAsync();
        return app;
    }

    [Fact]
    [Trait("Rule", "AUTH-01")]
    public async Task AUTH01_users_table_is_in_the_identity_schema()
    {
        await using var app = await StartAsync();

        Assert.True(await PostgresServer.ScalarAsync<bool>(app.ConnectionString, "SELECT to_regclass('identity.users') IS NOT NULL"));
    }

    [Fact]
    [Trait("Rule", "AUTH-01")]
    public async Task AUTH01_phone_number_belongs_to_one_user()
    {
        await using var app = await StartAsync();
        await app.AddUserAsync(phone: "+989121234567");

        await Assert.ThrowsAsync<DbUpdateException>(() => app.AddUserAsync(phone: "+989121234567"));
    }

    [Fact]
    [Trait("Rule", "AUTH-01")]
    public async Task AUTH01_username_is_unique_without_regard_to_case()
    {
        await using var app = await StartAsync();
        await app.AddUserAsync(username: "Sara");

        await Assert.ThrowsAsync<DbUpdateException>(() => app.AddUserAsync(username: "sara"));
    }

    [Fact]
    [Trait("Rule", "AUTH-02")]
    public async Task AUTH02_directory_gives_the_public_profile()
    {
        await using var app = await StartAsync();
        var id = await app.AddUserAsync("Sara", "sara");

        var profile = await app.Services.GetRequiredService<IUserDirectory>().FindAsync(id, TestContext.Current.CancellationToken);

        Assert.Equal(new UserProfile(id, "Sara", "sara"), profile);
    }

    [Fact]
    [Trait("Rule", "AUTH-02")]
    public async Task AUTH02_directory_gives_nothing_for_an_unknown_user()
    {
        await using var app = await StartAsync();

        Assert.Null(await app.Services.GetRequiredService<IUserDirectory>().FindAsync(404, TestContext.Current.CancellationToken));
    }

    [Fact]
    [Trait("Rule", "AUTH-02")]
    public void AUTH02_contract_has_no_phone_member()
    {
        var members = typeof(IUserDirectory).Assembly.GetTypes().SelectMany(type => type.GetMembers());

        Assert.DoesNotContain(members, member => member.Name.Contains("phone", StringComparison.OrdinalIgnoreCase));
    }
}
