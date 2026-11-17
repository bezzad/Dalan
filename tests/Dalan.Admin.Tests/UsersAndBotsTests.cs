using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dalan.Testing;

namespace Dalan.Admin.Tests;

public class UsersAndBotsTests(PostgresServer postgres)
{
    [Fact]
    [Trait("Rule", "ADM-11")]
    public async Task ADM11_find_by_phone_in_any_form()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();
        var (_, userId) = await app.LoginAsync("Sara", "09121234567");

        var user = Assert.Single((await admin.JsonAsync("/api/admin/users?q=0912 123 4567")).Rows("users"));

        Assert.Equal(userId, user.GetProperty("id").GetInt64());
        Assert.Equal("+989121234567", user.GetProperty("phone").GetString());
        Assert.Equal("Sara", user.GetProperty("name").GetString());
        Assert.Equal("none", user.GetProperty("builder").GetString());
        Assert.False(user.GetProperty("suspended").GetBoolean());
    }

    [Fact]
    [Trait("Rule", "ADM-11")]
    public async Task ADM11_find_by_id_username_and_name_start()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();
        var id = await app.AddUserAsync("Maryam Ahmadi", username: "maryam_a");

        foreach (var query in new[] { id.ToString(System.Globalization.CultureInfo.InvariantCulture), "Maryam_A", "Mary" })
        {
            var user = Assert.Single((await admin.JsonAsync($"/api/admin/users?q={Uri.EscapeDataString(query)}")).Rows("users"));
            Assert.Equal(id, user.GetProperty("id").GetInt64());
        }
    }

    [Fact]
    [Trait("Rule", "ADM-11")]
    public async Task ADM11_open_requests_oldest_first()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();
        var (first, firstId) = await app.LoginAsync("First");
        var (second, secondId) = await app.LoginAsync("Second");
        (await second.PostAsync("/api/account/builder", null, AdminApp.Cancel)).EnsureSuccessStatusCode();
        app.Time.Advance(TimeSpan.FromMinutes(1));
        (await first.PostAsync("/api/account/builder", null, AdminApp.Cancel)).EnsureSuccessStatusCode();

        var users = (await admin.JsonAsync("/api/admin/users?builder=requested")).Rows("users");

        Assert.Equal([secondId, firstId], users.Select(u => u.GetProperty("id").GetInt64()));
        Assert.All(users, u => Assert.Equal("requested", u.GetProperty("builder").GetString()));
    }

    [Fact]
    [Trait("Rule", "ADM-11")]
    public async Task ADM11_unknown_user_gets_404()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();

        var response = await admin.PostAsJsonAsync("/api/admin/users/999/suspend", new { suspended = true }, AdminApp.Cancel);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("user_not_found", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "ADM-11")]
    public async Task ADM11_admin_cannot_suspend_themself()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, adminId) = await app.AdminAsync();

        var response = await admin.PostAsJsonAsync($"/api/admin/users/{adminId}/suspend", new { suspended = true }, AdminApp.Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/me", AdminApp.Cancel)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "ADM-12")]
    public async Task ADM12_find_by_owner_id_and_username()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();
        await app.AddBotAsync("first_bot", owner: 7);
        await app.AddBotAsync("second_bot", owner: 7);
        await app.AddBotAsync("other_bot", owner: 8);

        var byOwner = (await admin.JsonAsync("/api/admin/bots?q=7")).Rows("bots");
        var byName = Assert.Single((await admin.JsonAsync("/api/admin/bots?q=@Second_Bot")).Rows("bots"));

        Assert.Equal(["first_bot", "second_bot"], byOwner.Select(b => b.GetProperty("username").GetString()));
        Assert.Equal(7, byName.GetProperty("owner_user_id").GetInt64());
        Assert.False(byName.GetProperty("suspended").GetBoolean());
    }

    [Fact]
    [Trait("Rule", "ADM-12")]
    public async Task ADM12_suspend_stops_the_bot()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();
        var (botId, token) = await app.AddBotAsync();
        var bot = app.CreateClient();
        bot.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var answer = await (await admin.PostAsJsonAsync($"/api/admin/bots/{botId}/suspend", new { suspended = true }, AdminApp.Cancel)).JsonAsync();
        var call = await bot.GetAsync("/v1/updates?timeout=0", AdminApp.Cancel);

        Assert.True(answer.GetProperty("suspended").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, call.StatusCode);
        Assert.Equal("bot_suspended", await call.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "ADM-12")]
    public async Task ADM12_unknown_bot_gets_404()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();

        var response = await admin.PostAsJsonAsync("/api/admin/bots/999/suspend", new { suspended = true }, AdminApp.Cancel);

        Assert.Equal("bot_not_found", await response.ErrorCodeAsync());
    }
}
