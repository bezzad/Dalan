using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dalan.Relay.Chats;
using Dalan.Testing;

namespace Dalan.Relay.Tests;

public class SendMessageTests(PostgresServer postgres)
{
    [Fact]
    [Trait("Rule", "REL-08")]
    public async Task REL08_bot_message_is_saved_and_returned()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, token) = await app.AddBotAsync();
        var user = await app.AddUserAsync();
        await app.UserSaysAsync(botId, user, "/start");

        var response = await app.BotClient(token).SendAsync(user, "hello");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(2, json.GetProperty("id").GetInt64());
        Assert.Equal(user, json.GetProperty("user_id").GetInt64());
        Assert.Equal("hello", json.GetProperty("text").GetString());
        Assert.Equal(app.Time.GetUtcNow(), json.GetProperty("date").GetDateTimeOffset());

        var stored = (await app.MessagesAsync())[^1];
        Assert.Equal((botId, user, 2, Direction.FromBot, "hello"),
            (stored.BotId, stored.UserId, stored.Seq, stored.Direction, stored.Content.Text));
        Assert.Null(stored.Delivery);
    }

    [Fact]
    [Trait("Rule", "REL-08")]
    public async Task REL08_bot_cannot_write_to_a_user_who_never_started_it()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (_, token) = await app.AddBotAsync();
        var user = await app.AddUserAsync();

        var response = await app.BotClient(token).SendAsync(user, "spam");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("chat_not_active", await response.ErrorCodeAsync());
        Assert.Empty(await app.MessagesAsync());
    }

    [Fact]
    [Trait("Rule", "REL-08")]
    public async Task REL08_bot_cannot_write_to_the_user_of_another_bot()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (otherBot, _) = await app.AddBotAsync("other");
        var (_, token) = await app.AddBotAsync("mine");
        var user = await app.AddUserAsync();
        await app.UserSaysAsync(otherBot, user, "/start");

        var response = await app.BotClient(token).SendAsync(user, "spam");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    [Trait("Rule", "REL-08")]
    public async Task REL08_bot_cannot_write_to_a_user_who_stopped_it()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (botId, token) = await app.AddBotAsync();
        var user = await app.AddUserAsync();
        await app.UserSaysAsync(botId, user, "/start");
        await PostgresServer.ExecuteAsync(app.ConnectionString, $"UPDATE relay.memberships SET state = {(int)MembershipState.Stopped}");

        var response = await app.BotClient(token).SendAsync(user, "come back");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("chat_not_active", await response.ErrorCodeAsync());
    }

    [Theory]
    [Trait("Rule", "REL-08")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"text":"no user"}""")]
    [InlineData("""{"user_id":"seven","text":"hello"}""")]
    [InlineData("""{"user_id":1}""")]
    public async Task REL08_body_that_is_not_valid_is_400(string body)
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (_, token) = await app.AddBotAsync();
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await app.BotClient(token).PostAsync("/v1/messages", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_request", await response.ErrorCodeAsync());
    }

    [Theory]
    [Trait("Rule", "REL-09")]
    [InlineData(0, "text_empty")]
    [InlineData(11, "text_too_long")]
    public async Task REL09_bot_text_must_fit_the_limit(int length, string expectedError)
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Relay:MaxTextLength", "10"));
        var (botId, token) = await app.AddBotAsync();
        var user = await app.AddUserAsync();
        await app.UserSaysAsync(botId, user, "/start");

        var response = await app.BotClient(token).SendAsync(user, new string('x', length));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expectedError, await response.ErrorCodeAsync());
        Assert.Single(await app.MessagesAsync());
    }

    [Fact]
    [Trait("Rule", "REL-11")]
    public async Task REL11_every_error_has_a_code_and_a_message()
    {
        await using var app = await RelayApp.StartAsync(postgres, ("Relay:BotRequestLimit", "3"), ("Relay:RateLimitWindowSeconds", "3600"));
        var (_, token) = await app.AddBotAsync();
        var bot = app.BotClient(token);

        HttpResponseMessage[] errors =
        [
            await app.BotClient(token: null).GetAsync("/v1/updates", TestContext.Current.CancellationToken), // 401
            await bot.GetAsync("/v1/updates?timeout=x", TestContext.Current.CancellationToken),              // 400
            await bot.SendAsync(userId: 404, "hello"),                                                          // 403
            await bot.SendAsync(userId: 404, ""),                                                               // 400
            await bot.GetAsync("/v1/updates?timeout=0", TestContext.Current.CancellationToken),              // 429
        ];

        Assert.Equal([401, 400, 403, 400, 429], errors.Select(e => (int)e.StatusCode));
        foreach (var error in errors)
            Assert.False(string.IsNullOrWhiteSpace(await error.ErrorCodeAsync()));
    }
}
