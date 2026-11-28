using Dalan.Identity.Contracts;
using Dalan.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Identity.Tests;

public class UserTests(PostgresServer postgres)
{
    private async Task<DalanApp> StartAsync(params (string Key, string Value)[] settings)
    {
        var app = new DalanApp(await postgres.NewDatabaseAsync(), settings.ToDictionary(s => s.Key, s => s.Value));
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
        // IUserAdmin and AdminUser carry the phone on purpose: only the Admin service uses them (ADM-11).
        // IUserPhones too: only Notification uses it, for the SMS of a notice (NTF-13).
        var members = typeof(IUserDirectory).Assembly.GetTypes()
            .Where(type => type != typeof(IUserAdmin) && type != typeof(AdminUser) && type != typeof(IUserPhones))
            .SelectMany(type => type.GetMembers());

        Assert.DoesNotContain(members, member => member.Name.Contains("phone", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Rule", "AUTH-02")]
    public void AUTH02_only_the_admin_service_uses_the_contract_with_phones()
    {
        const System.Reflection.BindingFlags All = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly;
        var others = new[] { typeof(Dalan.Relay.RelayOptions).Assembly, typeof(Dalan.Notification.NotificationOptions).Assembly };

        var users = others.SelectMany(a => a.GetTypes())
            .SelectMany(type => type.GetConstructors(All).Cast<System.Reflection.MethodBase>().Concat(type.GetMethods(All)))
            .Where(method => method.GetParameters().Any(p => p.ParameterType == typeof(IUserAdmin)))
            .Select(method => $"{method.DeclaringType}.{method.Name}");

        Assert.Empty(users);
    }

    [Fact]
    [Trait("Rule", "AUTH-02")]
    public void AUTH02_only_the_notification_service_uses_the_phone_of_one_user()
    {
        const System.Reflection.BindingFlags All = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly;
        var others = new[] { typeof(Dalan.Relay.RelayOptions).Assembly, typeof(Dalan.Sites.SitesOptions).Assembly, typeof(Dalan.Admin.AdminOptions).Assembly };

        var users = others.SelectMany(a => a.GetTypes())
            .SelectMany(type => type.GetConstructors(All).Cast<System.Reflection.MethodBase>().Concat(type.GetMethods(All)))
            .Where(method => method.GetParameters().Any(p => p.ParameterType == typeof(IUserPhones)))
            .Select(method => $"{method.DeclaringType}.{method.Name}");

        Assert.Empty(users);
    }

    [Fact]
    [Trait("Rule", "AUTH-02")]
    [Trait("Rule", "SITE-25")]
    public async Task AUTH02_directory_gives_the_admin_ids()
    {
        await using var app = await StartAsync(("Identity:AdminPhones", "09120001111, 09120002222"));
        await app.AddUserAsync("Common");
        var admin = await app.AddUserAsync("Admin", phone: "+989120002222");

        var ids = await app.Services.GetRequiredService<IUserDirectory>().AdminIdsAsync(TestContext.Current.CancellationToken);

        Assert.Equal([admin], ids);
    }
}
