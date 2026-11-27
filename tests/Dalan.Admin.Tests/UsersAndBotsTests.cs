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
    public async Task ADM11_find_by_email_in_any_case()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();
        await app.LoginByEmailAsync("sara@example.com");

        var user = Assert.Single((await admin.JsonAsync("/api/admin/users?q=SARA@example.com")).Rows("users"));

        Assert.Equal("sara@example.com", user.GetProperty("email").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, user.GetProperty("phone").ValueKind);
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

        Assert.Equal(["second_bot", "first_bot"], byOwner.Select(b => b.GetProperty("username").GetString()));
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

    [Fact]
    [Trait("Rule", "ADM-11")]
    public async Task ADM11_everyone_a_page_at_a_time_newest_first()
    {
        await using var app = await AdminApp.StartAsync(postgres, ("Admin:PageSize", "2"));
        var (admin, adminId) = await app.AdminAsync();
        var first = await app.AddUserAsync("First");
        var second = await app.AddUserAsync("Second");

        var page = (await admin.JsonAsync("/api/admin/users")).Rows("users");
        var next = (await admin.JsonAsync($"/api/admin/users?before={page[^1].GetProperty("id").GetInt64()}")).Rows("users");

        Assert.Equal([second, first], page.Select(u => u.GetProperty("id").GetInt64()));
        Assert.Equal([adminId], next.Select(u => u.GetProperty("id").GetInt64()));
    }

    [Fact]
    [Trait("Rule", "ADM-11")]
    public async Task ADM11_search_is_newest_first_and_pages_with_before()
    {
        await using var app = await AdminApp.StartAsync(postgres, ("Admin:PageSize", "2"));
        var (admin, _) = await app.AdminAsync();
        var first = await app.AddUserAsync("Maryam One");
        var second = await app.AddUserAsync("Maryam Two");
        var third = await app.AddUserAsync("Maryam Three");

        var page = (await admin.JsonAsync("/api/admin/users?q=Maryam")).Rows("users");
        var next = (await admin.JsonAsync($"/api/admin/users?q=Maryam&before={second}")).Rows("users");

        Assert.Equal([third, second], page.Select(u => u.GetProperty("id").GetInt64()));
        Assert.Equal([first], next.Select(u => u.GetProperty("id").GetInt64()));
    }

    [Fact]
    [Trait("Rule", "ADM-11")]
    public async Task ADM11_bad_before_is_400()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();

        var users = await admin.GetAsync("/api/admin/users?before=abc", AdminApp.Cancel);
        var bots = await admin.GetAsync("/api/admin/bots?before=-1", AdminApp.Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, users.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, bots.StatusCode);
    }

    [Fact]
    [Trait("Rule", "ADM-11")]
    public async Task ADM11_bot_count_and_last_active()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();
        var (_, ownerId) = await app.LoginAsync("Owner");
        var idle = await app.AddUserAsync("Idle");
        await app.AddBotAsync("first_bot", owner: ownerId);
        await app.AddBotAsync("second_bot", owner: ownerId);

        var users = (await admin.JsonAsync("/api/admin/users")).Rows("users");
        var user = users.Single(u => u.GetProperty("id").GetInt64() == ownerId);
        var other = users.Single(u => u.GetProperty("id").GetInt64() == idle);

        Assert.Equal(2, user.GetProperty("bots").GetInt32());
        Assert.Equal(app.Time.GetUtcNow(), user.GetProperty("last_active_at").GetDateTimeOffset());
        Assert.Equal(0, other.GetProperty("bots").GetInt32());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, other.GetProperty("last_active_at").ValueKind);
    }

    [Fact]
    [Trait("Rule", "ADM-12")]
    public async Task ADM12_everyone_newest_first_with_users()
    {
        await using var app = await AdminApp.StartAsync(postgres, ("Admin:PageSize", "2"));
        var (admin, _) = await app.AdminAsync();
        var (first, _) = await app.AddBotAsync("first_bot");
        var (second, _) = await app.AddBotAsync("second_bot");
        var (third, _) = await app.AddBotAsync("third_bot");
        var (sara, _) = await app.LoginAsync("Sara");
        var (ali, _) = await app.LoginAsync("Ali");
        await sara.SayAsync(third, "/start");
        await ali.SayAsync(third, "/start");
        await sara.SayAsync(first, "/start");

        var page = (await admin.JsonAsync("/api/admin/bots")).Rows("bots");
        var next = (await admin.JsonAsync($"/api/admin/bots?before={second}")).Rows("bots");

        Assert.Equal([third, second], page.Select(b => b.GetProperty("id").GetInt64()));
        Assert.Equal([2, 0], page.Select(b => b.GetProperty("users").GetInt32()));
        var last = Assert.Single(next);
        Assert.Equal(first, last.GetProperty("id").GetInt64());
        Assert.Equal(1, last.GetProperty("users").GetInt32());
    }

    [Fact]
    [Trait("Rule", "ADM-20")]
    public async Task ADM20_a_user_with_bots()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();
        var (_, userId) = await app.LoginAsync("Owner");
        var (botA, _) = await app.AddBotAsync("first_bot", owner: userId);
        var (botB, _) = await app.AddBotAsync("second_bot", owner: userId);
        await app.AddBotAsync("other_bot", owner: userId + 100);

        var body = await admin.JsonAsync($"/api/admin/users/{userId}");

        var user = body.GetProperty("user");
        Assert.Equal(userId, user.GetProperty("id").GetInt64());
        Assert.Equal("Owner", user.GetProperty("name").GetString());
        Assert.Equal(2, user.GetProperty("bots").GetInt32());
        var bots = body.Rows("bots");
        Assert.Equal([botB, botA], bots.Select(b => b.GetProperty("id").GetInt64()));
        Assert.All(bots, b => Assert.Equal(0, b.GetProperty("users").GetInt32()));
    }

    [Fact]
    [Trait("Rule", "ADM-20")]
    public async Task ADM20_a_bot_and_its_owner()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();
        var (_, userId) = await app.LoginAsync("Owner");
        var (botId, _) = await app.AddBotAsync("first_bot", owner: userId);

        var body = await admin.JsonAsync($"/api/admin/bots/{botId}");

        var bot = body.GetProperty("bot");
        Assert.Equal(botId, bot.GetProperty("id").GetInt64());
        Assert.Equal("first_bot", bot.GetProperty("username").GetString());
        Assert.Equal(userId, bot.GetProperty("owner_user_id").GetInt64());
        Assert.Equal(0, bot.GetProperty("users").GetInt32());
        var owner = body.GetProperty("owner");
        Assert.Equal(userId, owner.GetProperty("id").GetInt64());
        Assert.Equal(1, owner.GetProperty("bots").GetInt32());
    }

    [Fact]
    [Trait("Rule", "ADM-20")]
    public async Task ADM20_unknown_ids_get_404()
    {
        await using var app = await AdminApp.StartAsync(postgres);
        var (admin, _) = await app.AdminAsync();

        var user = await admin.GetAsync("/api/admin/users/999", AdminApp.Cancel);
        var bot = await admin.GetAsync("/api/admin/bots/999", AdminApp.Cancel);

        Assert.Equal(HttpStatusCode.NotFound, user.StatusCode);
        Assert.Equal("user_not_found", await user.ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.NotFound, bot.StatusCode);
        Assert.Equal("bot_not_found", await bot.ErrorCodeAsync());
    }
}
