using System.Net;
using Dalan.Testing;

namespace Dalan.Admin.Tests;

public class AdminDoorTests(PostgresServer postgres)
{
    [Fact]
    [Trait("Rule", "ADM-05")]
    public async Task ADM05_no_session_gets_401()
    {
        await using var app = await AdminApp.StartAsync(postgres);

        var response = await app.Browser().GetAsync("/api/admin/settings", AdminApp.Cancel);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Rule", "ADM-05")]
    public async Task ADM05_other_user_gets_403_not_admin()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (browser, _) = await app.LoginAsync("Sara", "09121111111");

        var response = await browser.GetAsync("/api/admin/settings", AdminApp.Cancel);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("not_admin", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "ADM-05")]
    public async Task ADM05_admin_call_works_and_is_in_the_access_log()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, adminId) = await app.AdminAsync();
        DalanApp.From(admin, "203.0.113.7:51000");

        await admin.JsonAsync("/api/admin/settings");

        Assert.Contains($"User {adminId} 203.0.113.7 51000", await app.AccessLogAsync());
    }
}
