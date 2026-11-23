using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Testing;

namespace Dalan.Relay.Tests;

/// <summary>REL-51: one icon for each bot.</summary>
public class BotIconTests(PostgresServer postgres)
{
    private const string AdminPhone = "09129990001";

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 9, 9];

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private sealed record World(DalanApp App, HttpClient Admin, HttpClient Builder, long BotId, string Token) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => App.DisposeAsync();
    }

    private async Task<World> StartAsync(params (string Key, string Value)[] settings)
    {
        var app = await RelayApp.StartAsync(postgres, [.. settings, ("Identity:AdminPhones", AdminPhone)]);
        var (admin, _) = await app.LoginAsync("Owner", AdminPhone);
        var (builder, builderId) = await app.LoginAsync("Builder");
        (await admin.PostAsJsonAsync($"/api/admin/users/{builderId}/builder", new { approve = true }, Cancel)).EnsureSuccessStatusCode();
        var made = await (await builder.PostAsJsonAsync("/api/builder/bots", new { username = "game_bot", name = "Game" }, Cancel))
            .Content.ReadFromJsonAsync<JsonElement>(Cancel);
        return new World(app, admin, builder, made.GetProperty("id").GetInt64(), made.GetProperty("token").GetString()!);
    }

    private static Task<HttpResponseMessage> PutAsync(HttpClient client, long botId, byte[] bytes, string type = "application/octet-stream")
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(type);
        return client.PutAsync($"/api/builder/bots/{botId}/icon", content, Cancel);
    }

    private static async Task<int> VersionInListAsync(HttpClient user, long botId) =>
        (await user.JsonAsync("/api/bots")).GetProperty("bots").EnumerateArray()
            .Single(b => b.GetProperty("id").GetInt64() == botId).GetProperty("icon_version").GetInt32();

    [Fact]
    [Trait("Rule", "REL-51")]
    public async Task REL51_owner_sets_the_icon_and_anyone_reads_it_without_a_session()
    {
        await using var world = await StartAsync();
        var (user, _) = await world.App.LoginAsync("Reader");
        Assert.Equal(HttpStatusCode.OK, (await user.SayAsync(world.BotId, "/start")).StatusCode);
        Assert.Equal(0, await VersionInListAsync(user, world.BotId));

        var response = await PutAsync(world.Builder, world.BotId, Png);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, (await response.Content.ReadFromJsonAsync<JsonElement>(Cancel)).GetProperty("icon_version").GetInt32());
        var anonymous = await world.App.CreateClient().GetAsync($"/api/bots/{world.BotId}/icon?v=1", Cancel);
        Assert.Equal(HttpStatusCode.OK, anonymous.StatusCode);
        Assert.Equal("image/png", anonymous.Content.Headers.ContentType?.MediaType);
        Assert.Contains("max-age", anonymous.Headers.CacheControl?.ToString(), StringComparison.Ordinal);
        Assert.Equal(Png, await anonymous.Content.ReadAsByteArrayAsync(Cancel));
        Assert.Equal(1, await VersionInListAsync(user, world.BotId));
        Assert.Equal(1, (await user.JsonAsync("/api/bots/by-username/game_bot")).GetProperty("icon_version").GetInt32());
        var own = (await world.Builder.JsonAsync("/api/builder/bots")).GetProperty("bots")[0];
        Assert.Equal(1, own.GetProperty("icon_version").GetInt32());
    }

    [Fact]
    [Trait("Rule", "REL-51")]
    public async Task REL51_every_change_raises_the_version_and_the_type_comes_from_the_bytes()
    {
        await using var world = await StartAsync();
        (await PutAsync(world.Builder, world.BotId, Png)).EnsureSuccessStatusCode();

        var again = await PutAsync(world.Builder, world.BotId, Jpeg, "image/png");

        Assert.Equal(2, (await again.Content.ReadFromJsonAsync<JsonElement>(Cancel)).GetProperty("icon_version").GetInt32());
        var read = await world.App.CreateClient().GetAsync($"/api/bots/{world.BotId}/icon", Cancel);
        Assert.Equal("image/jpeg", read.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Jpeg, await read.Content.ReadAsByteArrayAsync(Cancel));
        Assert.Equal(2, await PostgresServer.ScalarAsync<long>(world.App.ConnectionString,
            "SELECT count(*) FROM admin.audit_log WHERE action = 'bot.icon'"));
    }

    [Theory]
    [Trait("Rule", "REL-51")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>")]
    [InlineData("just a text")]
    [InlineData("")]
    public async Task REL51_not_an_image_is_400_and_the_old_icon_stays(string body)
    {
        await using var world = await StartAsync();
        (await PutAsync(world.Builder, world.BotId, Png)).EnsureSuccessStatusCode();

        var response = await PutAsync(world.Builder, world.BotId, System.Text.Encoding.UTF8.GetBytes(body), "image/png");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_icon", await response.ErrorCodeAsync());
        Assert.Equal(Png, await world.App.CreateClient().GetByteArrayAsync($"/api/bots/{world.BotId}/icon", Cancel));
    }

    [Fact]
    [Trait("Rule", "REL-51")]
    public async Task REL51_icon_over_the_limit_is_400()
    {
        await using var world = await StartAsync(("Relay:MaxBotIconKb", "1"));

        var response = await PutAsync(world.Builder, world.BotId, [.. Png, .. new byte[2048]]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_icon", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "REL-51")]
    public async Task REL51_remove_gives_404_and_version_0_and_a_new_icon_gets_a_new_version()
    {
        await using var world = await StartAsync();
        var (user, _) = await world.App.LoginAsync("Reader");
        await user.SayAsync(world.BotId, "/start");
        (await PutAsync(world.Builder, world.BotId, Png)).EnsureSuccessStatusCode();

        var removed = await world.Builder.DeleteAsync($"/api/builder/bots/{world.BotId}/icon", Cancel);

        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.App.CreateClient().GetAsync($"/api/bots/{world.BotId}/icon", Cancel)).StatusCode);
        Assert.Equal(0, await VersionInListAsync(user, world.BotId));
        (await PutAsync(world.Builder, world.BotId, Jpeg)).EnsureSuccessStatusCode();
        Assert.True(await VersionInListAsync(user, world.BotId) > 1, "a new picture must not reuse the cache key of the old one");
    }

    [Fact]
    [Trait("Rule", "REL-51")]
    public async Task REL51_another_builder_gets_404_and_an_admin_may_change_it()
    {
        await using var world = await StartAsync();
        var (other, otherId) = await world.App.LoginAsync("Other");
        (await world.Admin.PostAsJsonAsync($"/api/admin/users/{otherId}/builder", new { approve = true }, Cancel)).EnsureSuccessStatusCode();

        var refused = await PutAsync(other, world.BotId, Png);
        var byAdmin = await PutAsync(world.Admin, world.BotId, Png);

        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        Assert.Equal("bot_not_found", await refused.ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.OK, byAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/builder/bots/{world.BotId}/icon", Cancel)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "REL-51")]
    public async Task REL51_suspended_or_deleted_bot_has_no_public_icon()
    {
        await using var world = await StartAsync();
        (await PutAsync(world.Builder, world.BotId, Png)).EnsureSuccessStatusCode();
        var anonymous = world.App.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/bots/{world.BotId}/icon", Cancel)).StatusCode);

        (await world.Admin.PostAsJsonAsync($"/api/admin/bots/{world.BotId}/suspend", new { suspended = true }, Cancel)).EnsureSuccessStatusCode();
        var suspended = await anonymous.GetAsync($"/api/bots/{world.BotId}/icon", Cancel);
        (await world.Admin.PostAsJsonAsync($"/api/admin/bots/{world.BotId}/suspend", new { suspended = false }, Cancel)).EnsureSuccessStatusCode();
        (await world.Builder.DeleteAsync($"/api/builder/bots/{world.BotId}", Cancel)).EnsureSuccessStatusCode();
        var deleted = await anonymous.GetAsync($"/api/bots/{world.BotId}/icon", Cancel);

        Assert.Equal(HttpStatusCode.NotFound, suspended.StatusCode);
        Assert.Equal("not_found", await suspended.ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);
    }

    [Fact]
    [Trait("Rule", "REL-51")]
    [Trait("Rule", "REL-18")]
    public async Task REL51_settings_tell_the_client_the_icon_limit()
    {
        await using var world = await StartAsync(("Relay:MaxBotIconKb", "77"));

        var settings = await world.Builder.JsonAsync("/api/settings");

        Assert.Equal(77, settings.GetProperty("max_bot_icon_kb").GetInt32());
    }
}
