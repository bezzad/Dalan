using System.Net;
using Dalan.Admin.Contracts;
using Dalan.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Admin.Tests;

public class AccessLogSearchTests(PostgresServer postgres)
{
    private static ValueTask RecordAsync(DalanApp app, AccessActor actor, long id, string endPoint)
    {
        var at = System.Net.IPEndPoint.Parse(endPoint);
        return app.Services.GetRequiredService<IAccessLog>().RecordAsync(actor, id, at.Address, at.Port, AdminApp.Cancel);
    }

    [Fact]
    [Trait("Rule", "ADM-10")]
    public async Task ADM10_by_bot()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();
        await RecordAsync(app, AccessActor.Bot, 5, "203.0.113.7:51000");
        await RecordAsync(app, AccessActor.Bot, 6, "198.51.100.1:1000");

        var rows = (await admin.JsonAsync("/api/admin/access-log?bot=5")).Rows();

        var row = Assert.Single(rows);
        Assert.Equal("bot", row.GetProperty("actor").GetString());
        Assert.Equal(5, row.GetProperty("actor_id").GetInt64());
        Assert.Equal("203.0.113.7", row.GetProperty("address").GetString());
        Assert.Equal(51000, row.GetProperty("port").GetInt32());
        Assert.Equal(app.Time.GetUtcNow(), row.GetProperty("at").GetDateTimeOffset());
    }

    [Fact]
    [Trait("Rule", "ADM-10")]
    public async Task ADM10_by_address_folds_ipv4_written_as_ipv6()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();
        await RecordAsync(app, AccessActor.User, 7, "203.0.113.7:1");
        await RecordAsync(app, AccessActor.Bot, 5, "203.0.113.7:2");
        await RecordAsync(app, AccessActor.Bot, 6, "198.51.100.1:3");

        var rows = (await admin.JsonAsync("/api/admin/access-log?address=::ffff:203.0.113.7")).Rows();

        Assert.Equal(["bot 5", "user 7"], rows.Select(r => $"{r.GetProperty("actor").GetString()} {r.GetProperty("actor_id").GetInt64()}"));
    }

    [Fact]
    [Trait("Rule", "ADM-10")]
    public async Task ADM10_by_phone_in_any_form()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();
        await app.Services.GetRequiredService<IAccessLog>()
            .RecordSmsAsync("+989121234567", System.Net.IPAddress.Parse("203.0.113.7"), 51000, AdminApp.Cancel);

        var row = Assert.Single((await admin.JsonAsync("/api/admin/access-log?phone=09121234567")).Rows());

        Assert.Equal("phone", row.GetProperty("actor").GetString());
        Assert.Equal("+989121234567", row.GetProperty("phone").GetString());
    }

    [Fact]
    [Trait("Rule", "ADM-10")]
    public async Task ADM10_by_user_with_paging()
    {
        await using var app = await AdminApp.StartAsync(postgres, ("Admin:PageSize", "1"));
        var (admin, _) = await app.AdminAsync();
        await RecordAsync(app, AccessActor.User, 7, "203.0.113.1:1");
        await RecordAsync(app, AccessActor.User, 7, "203.0.113.2:1");

        var first = Assert.Single((await admin.JsonAsync("/api/admin/access-log?user=7")).Rows());
        var second = Assert.Single((await admin.JsonAsync($"/api/admin/access-log?user=7&before={first.GetProperty("id").GetInt64()}")).Rows());

        Assert.Equal("203.0.113.2", first.GetProperty("address").GetString());
        Assert.Equal("203.0.113.1", second.GetProperty("address").GetString());
    }

    [Theory]
    [Trait("Rule", "ADM-10")]
    [InlineData("")]
    [InlineData("?user=1&bot=2")]
    [InlineData("?address=not-an-ip")]
    [InlineData("?user=abc")]
    [InlineData("?phone=123")]
    public async Task ADM10_no_filter_or_bad_filter_gets_400(string query)
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();

        var response = await admin.GetAsync($"/api/admin/access-log{query}", AdminApp.Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_request", await response.ErrorCodeAsync());
    }
}
