using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Testing;

namespace Dalan.Relay.Tests;

public class ShowcaseTests(PostgresServer postgres)
{
    private const string AdminPhone = "09129990001";

    /// <summary>The 8 bytes every PNG starts with, then some data.</summary>
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private sealed record World(DalanApp App, HttpClient Admin, HttpClient Builder) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => App.DisposeAsync();
    }

    private async Task<World> StartAsync(params (string Key, string Value)[] settings)
    {
        var app = await RelayApp.StartAsync(postgres, [.. settings, ("Identity:AdminPhones", AdminPhone)]);
        var (admin, _) = await app.LoginAsync("Owner", AdminPhone);
        var (builder, builderId) = await app.LoginAsync("Builder");
        (await admin.PostAsJsonAsync($"/api/admin/users/{builderId}/builder", new { approve = true }, Cancel)).EnsureSuccessStatusCode();
        return new World(app, admin, builder);
    }

    private static async Task<long> CreateBotAsync(HttpClient builder, string username)
    {
        var response = await builder.PostAsJsonAsync("/api/builder/bots", new { username, name = $"Name of {username}" }, Cancel);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Cancel)).GetProperty("id").GetInt64();
    }

    private static Task<HttpResponseMessage> AskAsync(HttpClient builder, long botId, string category = "games", string description = "A fun game") =>
        builder.PutAsJsonAsync($"/api/builder/bots/{botId}/showcase", new { description, category }, Cancel);

    private static Task<HttpResponseMessage> IconAsync(HttpClient builder, long botId, byte[] bytes, string type = "image/png")
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(type);
        return builder.PutAsync($"/api/builder/bots/{botId}/showcase/icon", content, Cancel);
    }

    private static Task<HttpResponseMessage> ReviewAsync(HttpClient admin, long botId, bool approve) =>
        admin.PostAsJsonAsync($"/api/admin/showcase/{botId}", new { approve }, Cancel);

    /// <summary>A bot asked and the admin approved it.</summary>
    private static async Task<long> ListedBotAsync(World world, string username, string category = "games")
    {
        var botId = await CreateBotAsync(world.Builder, username);
        (await AskAsync(world.Builder, botId, category)).EnsureSuccessStatusCode();
        (await ReviewAsync(world.Admin, botId, approve: true)).EnsureSuccessStatusCode();
        return botId;
    }

    /// <summary>The public list, asked with no session.</summary>
    private static async Task<JsonElement[]> PublicAsync(DalanApp app, string query = "") =>
        [.. (await app.CreateClient().JsonAsync($"/api/showcase{query}")).GetProperty("bots").EnumerateArray()];

    private static async Task<JsonElement?> OwnEntryAsync(HttpClient builder, long botId)
    {
        var bot = (await builder.JsonAsync("/api/builder/bots")).GetProperty("bots").EnumerateArray()
            .Single(b => b.GetProperty("id").GetInt64() == botId);
        var showcase = bot.GetProperty("showcase");
        return showcase.ValueKind == JsonValueKind.Null ? null : showcase;
    }

    [Fact]
    [Trait("Rule", "REL-40")]
    public async Task REL40_builder_asks_and_the_entry_is_pending()
    {
        await using var world = await StartAsync();
        var botId = await CreateBotAsync(world.Builder, "game_bot");
        Assert.Null(await OwnEntryAsync(world.Builder, botId));

        var response = await AskAsync(world.Builder, botId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entry = (await OwnEntryAsync(world.Builder, botId))!.Value;
        Assert.Equal("pending", entry.GetProperty("state").GetString());
        Assert.Equal("A fun game", entry.GetProperty("description").GetString());
        Assert.Equal("games", entry.GetProperty("category").GetString());
        Assert.False(entry.GetProperty("icon").GetBoolean());
        var audit = (await world.Admin.JsonAsync("/api/admin/audit")).GetProperty("rows").EnumerateArray()
            .Select(r => r.GetProperty("action").GetString());
        Assert.Contains("showcase.request", audit);
    }

    [Fact]
    [Trait("Rule", "REL-40")]
    public async Task REL40_a_change_goes_back_to_review()
    {
        await using var world = await StartAsync();
        var botId = await ListedBotAsync(world, "game_bot");
        Assert.Single(await PublicAsync(world.App));

        (await AskAsync(world.Builder, botId, description: "A new text")).EnsureSuccessStatusCode();

        Assert.Equal("pending", (await OwnEntryAsync(world.Builder, botId))!.Value.GetProperty("state").GetString());
        Assert.Empty(await PublicAsync(world.App));
    }

    [Theory]
    [Trait("Rule", "REL-40")]
    [InlineData("casino", "A fun game")]
    [InlineData("games", "")]
    [InlineData("games", "ABCDEFGHIJK")]
    public async Task REL40_bad_category_or_description_gets_400(string category, string description)
    {
        await using var world = await StartAsync(("Relay:MaxBotDescriptionLength", "10"));
        var botId = await CreateBotAsync(world.Builder, "game_bot");

        var response = await AskAsync(world.Builder, botId, category, description);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_request", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "REL-40")]
    public async Task REL40_another_owners_bot_gets_404()
    {
        await using var world = await StartAsync();
        var botId = await CreateBotAsync(world.Builder, "game_bot");
        var (other, otherId) = await world.App.LoginAsync("Other");
        (await world.Admin.PostAsJsonAsync($"/api/admin/users/{otherId}/builder", new { approve = true }, Cancel)).EnsureSuccessStatusCode();

        var response = await AskAsync(other, botId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("bot_not_found", await response.ErrorCodeAsync());
        Assert.Null(await OwnEntryAsync(world.Builder, botId));
    }

    [Fact]
    [Trait("Rule", "REL-41")]
    public async Task REL41_png_icon_is_stored_and_goes_back_to_review()
    {
        await using var world = await StartAsync();
        var botId = await ListedBotAsync(world, "game_bot");

        var response = await IconAsync(world.Builder, botId, Png, "application/octet-stream");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entry = (await OwnEntryAsync(world.Builder, botId))!.Value;
        Assert.True(entry.GetProperty("icon").GetBoolean());
        Assert.Equal("pending", entry.GetProperty("state").GetString());
        var icon = await world.Builder.GetAsync($"/api/builder/bots/{botId}/showcase/icon", Cancel);
        Assert.Equal("image/png", icon.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await icon.Content.ReadAsByteArrayAsync(Cancel));
        var seenByAdmin = await world.Admin.GetAsync($"/api/builder/bots/{botId}/showcase/icon", Cancel);
        Assert.Equal(HttpStatusCode.OK, seenByAdmin.StatusCode);
    }

    [Theory]
    [Trait("Rule", "REL-41")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>", "image/svg+xml")]
    [InlineData("just a text", "image/png")]
    [InlineData("", "image/png")]
    public async Task REL41_not_an_image_gets_400(string body, string type)
    {
        await using var world = await StartAsync();
        var botId = await CreateBotAsync(world.Builder, "game_bot");
        (await AskAsync(world.Builder, botId)).EnsureSuccessStatusCode();

        var response = await IconAsync(world.Builder, botId, System.Text.Encoding.UTF8.GetBytes(body), type);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_icon", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "REL-41")]
    public async Task REL41_too_big_icon_gets_400()
    {
        await using var world = await StartAsync(("Relay:MaxBotIconKb", "1"));
        var botId = await CreateBotAsync(world.Builder, "game_bot");
        (await AskAsync(world.Builder, botId)).EnsureSuccessStatusCode();

        var response = await IconAsync(world.Builder, botId, [.. Png, .. new byte[2048]]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_icon", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "REL-41")]
    public async Task REL41_icon_without_an_entry_gets_404()
    {
        await using var world = await StartAsync();
        var botId = await CreateBotAsync(world.Builder, "game_bot");

        var response = await IconAsync(world.Builder, botId, Png);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("showcase_not_found", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "REL-42")]
    public async Task REL42_leaving_removes_the_bot_and_its_icon_at_once()
    {
        await using var world = await StartAsync();
        var botId = await CreateBotAsync(world.Builder, "game_bot");
        (await AskAsync(world.Builder, botId)).EnsureSuccessStatusCode();
        (await IconAsync(world.Builder, botId, Png)).EnsureSuccessStatusCode();
        (await ReviewAsync(world.Admin, botId, approve: true)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await world.App.CreateClient().GetAsync($"/api/showcase/{botId}/icon", Cancel)).StatusCode);

        var response = await world.Builder.DeleteAsync($"/api/builder/bots/{botId}/showcase", Cancel);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await PublicAsync(world.App));
        Assert.Equal(HttpStatusCode.NotFound, (await world.App.CreateClient().GetAsync($"/api/showcase/{botId}/icon", Cancel)).StatusCode);
        Assert.Null(await OwnEntryAsync(world.Builder, botId));
    }

    [Fact]
    [Trait("Rule", "REL-43")]
    public async Task REL43_only_listed_bots_are_public()
    {
        await using var world = await StartAsync();
        var listed = await ListedBotAsync(world, "listed_bot");
        (await IconAsync(world.Builder, listed, Png)).EnsureSuccessStatusCode();
        (await ReviewAsync(world.Admin, listed, approve: true)).EnsureSuccessStatusCode();
        var pending = await CreateBotAsync(world.Builder, "pending_bot");
        (await AskAsync(world.Builder, pending)).EnsureSuccessStatusCode();
        var refused = await CreateBotAsync(world.Builder, "refused_bot");
        (await AskAsync(world.Builder, refused)).EnsureSuccessStatusCode();
        (await ReviewAsync(world.Admin, refused, approve: false)).EnsureSuccessStatusCode();
        await CreateBotAsync(world.Builder, "private_bot");

        var bot = Assert.Single(await PublicAsync(world.App));

        Assert.Equal(listed, bot.GetProperty("id").GetInt64());
        Assert.Equal("listed_bot", bot.GetProperty("username").GetString());
        Assert.Equal("Name of listed_bot", bot.GetProperty("name").GetString());
        Assert.Equal("A fun game", bot.GetProperty("description").GetString());
        Assert.Equal("games", bot.GetProperty("category").GetString());
        Assert.True(bot.GetProperty("icon").GetBoolean());
        var icon = await world.App.CreateClient().GetAsync($"/api/showcase/{listed}/icon", Cancel);
        Assert.Equal(Png, await icon.Content.ReadAsByteArrayAsync(Cancel));
        Assert.Equal(HttpStatusCode.NotFound, (await world.App.CreateClient().GetAsync($"/api/showcase/{pending}/icon", Cancel)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "REL-43")]
    public async Task REL43_the_answer_has_the_categories_of_the_setting()
    {
        await using var world = await StartAsync(("Relay:ShowcaseCategories", "games, tools"));

        var body = await world.App.CreateClient().JsonAsync("/api/showcase");

        Assert.Equal("games,tools", string.Join(",", body.GetProperty("categories").EnumerateArray().Select(c => c.GetString())));
    }

    [Fact]
    [Trait("Rule", "REL-43")]
    public async Task REL43_suspended_bot_is_hidden()
    {
        await using var world = await StartAsync();
        var botId = await ListedBotAsync(world, "game_bot");
        (await IconAsync(world.Builder, botId, Png)).EnsureSuccessStatusCode();
        (await ReviewAsync(world.Admin, botId, approve: true)).EnsureSuccessStatusCode();

        (await world.Admin.PostAsJsonAsync($"/api/admin/bots/{botId}/suspend", new { suspended = true }, Cancel)).EnsureSuccessStatusCode();

        Assert.Empty(await PublicAsync(world.App));
        Assert.Equal(HttpStatusCode.NotFound, (await world.App.CreateClient().GetAsync($"/api/showcase/{botId}/icon", Cancel)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "REL-43")]
    public async Task REL43_one_category_page_by_page()
    {
        await using var world = await StartAsync(("Relay:ShowcasePageSize", "1"));
        var first = await ListedBotAsync(world, "first_game");
        await ListedBotAsync(world, "some_tool", "tools");
        var second = await ListedBotAsync(world, "second_game");

        var page1 = Assert.Single(await PublicAsync(world.App, "?category=games"));
        var page2 = Assert.Single(await PublicAsync(world.App, $"?category=games&after={page1.GetProperty("id").GetInt64()}"));

        Assert.Equal(first, page1.GetProperty("id").GetInt64());
        Assert.Equal(second, page2.GetProperty("id").GetInt64());
        Assert.Empty(await PublicAsync(world.App, $"?category=games&after={second}"));
    }

    [Fact]
    [Trait("Rule", "REL-43")]
    public async Task REL43_bad_after_gets_400()
    {
        await using var world = await StartAsync();

        var response = await world.App.CreateClient().GetAsync("/api/showcase?after=abc", Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
