using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Testing;
using static Dalan.Identity.Tests.Http;

namespace Dalan.Identity.Tests;

/// <summary>AUTH-38: an access key may work only from some addresses.</summary>
public class KeyAddressTests(PostgresServer postgres)
{
    private const string AdminPhone = "09129990001";

    private Task<DalanApp> StartAsync(params (string Key, string Value)[] settings) =>
        Http.StartAsync(postgres, [.. settings, ("Identity:AdminPhones", AdminPhone)]);

    /// <summary>A builder's browser and one of its keys.</summary>
    private static async Task<(HttpClient Browser, long UserId, long KeyId, string Key)> BuilderKeyAsync(DalanApp app)
    {
        var (admin, _) = await app.LoginAsync("Owner", AdminPhone);
        var (browser, userId) = await app.LoginAsync("Builder");
        (await admin.PostAsJsonAsync($"/api/admin/users/{userId}/builder", new { approve = true }, Cancel)).EnsureSuccessStatusCode();
        var made = await JsonAsync(await browser.PostAsJsonAsync("/api/account/keys", new { name = "laptop" }, Cancel));
        return (browser, userId, made.GetProperty("id").GetInt64(), made.GetProperty("key").GetString()!);
    }

    private static Task<HttpResponseMessage> SetAsync(HttpClient browser, long keyId, string[] addresses) =>
        browser.PutAsJsonAsync($"/api/account/keys/{keyId}/addresses", new { addresses }, Cancel);

    /// <summary>A terminal with the key, calling from <paramref name="endPoint"/>.</summary>
    private static Task<HttpResponseMessage> CallAsync(DalanApp app, string key, string endPoint)
    {
        var client = DalanApp.From(app.CreateClient(), endPoint);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return client.GetAsync("/api/builder/bots", Cancel);
    }

    private static string[] Addresses(JsonElement key) => [.. key.GetProperty("addresses").EnumerateArray().Select(a => a.GetString()!)];

    [Fact]
    [Trait("Rule", "AUTH-38")]
    public async Task AUTH38_set_the_list()
    {
        await using var app = await StartAsync();
        var (browser, userId, keyId, _) = await BuilderKeyAsync(app);
        Assert.Empty(Addresses(Assert.Single((await JsonAsync(await browser.GetAsync("/api/account/keys", Cancel))).GetProperty("keys").EnumerateArray())));

        var response = await SetAsync(browser, keyId, ["80.244.7.0/24", " 2a01::1 "]);
        var listed = Assert.Single((await JsonAsync(await browser.GetAsync("/api/account/keys", Cancel))).GetProperty("keys").EnumerateArray());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var answer = await JsonAsync(response);
        Assert.Equal(keyId, answer.GetProperty("id").GetInt64());
        Assert.Equal(["80.244.7.0/24", "2a01::1"], Addresses(answer));
        Assert.Equal(["80.244.7.0/24", "2a01::1"], Addresses(listed));
        Assert.Equal(1, await PostgresServer.ScalarAsync<long>(app.ConnectionString,
            $"SELECT count(*) FROM admin.audit_log WHERE action = 'key.addresses' AND user_id = {userId}"));
    }

    [Theory]
    [Trait("Rule", "AUTH-38")]
    [InlineData("80.244.7.0/33")]
    [InlineData("80.244.7.1/24")]
    [InlineData("example.com")]
    [InlineData("")]
    public async Task AUTH38_bad_address_gets_400(string address)
    {
        await using var app = await StartAsync();
        var (browser, _, keyId, _) = await BuilderKeyAsync(app);

        var response = await SetAsync(browser, keyId, ["1.2.3.4", address]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_address", await ErrorCodeAsync(response));
    }

    [Fact]
    [Trait("Rule", "AUTH-38")]
    public async Task AUTH38_more_than_the_limit_gets_400()
    {
        await using var app = await StartAsync(("Identity:MaxKeyAddresses", "2"));
        var (browser, _, keyId, _) = await BuilderKeyAsync(app);

        var response = await SetAsync(browser, keyId, ["1.2.3.4", "1.2.3.5", "1.2.3.6"]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("too_many_addresses", await ErrorCodeAsync(response));
    }

    [Fact]
    [Trait("Rule", "AUTH-38")]
    public async Task AUTH38_another_users_key_gets_404()
    {
        await using var app = await StartAsync();
        var (_, _, keyId, _) = await BuilderKeyAsync(app);
        var (bob, _) = await app.LoginAsync("Bob");

        var response = await SetAsync(bob, keyId, ["1.2.3.4"]);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("key_not_found", await ErrorCodeAsync(response));
    }

    [Fact]
    [Trait("Rule", "AUTH-38")]
    public async Task AUTH38_call_from_another_address_is_refused_and_logged()
    {
        await using var app = await StartAsync();
        var (browser, userId, keyId, key) = await BuilderKeyAsync(app);
        (await SetAsync(browser, keyId, ["80.244.7.0/24"])).EnsureSuccessStatusCode();

        var refused = await CallAsync(app, key, "5.6.7.8:41000");

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("key_address_refused", await ErrorCodeAsync(refused));
        Assert.Contains($"User {userId} 5.6.7.8 41000", await app.AccessLogAsync());
        Assert.Equal(HttpStatusCode.OK, (await CallAsync(app, key, "80.244.7.200:41000")).StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-38")]
    public async Task AUTH38_ipv6_and_mapped_ipv4_addresses_match()
    {
        await using var app = await StartAsync();
        var (browser, _, keyId, key) = await BuilderKeyAsync(app);
        (await SetAsync(browser, keyId, ["2a01::1", "80.244.7.0/24"])).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.OK, (await CallAsync(app, key, "[2a01::1]:41000")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await CallAsync(app, key, "[::ffff:80.244.7.9]:41000")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CallAsync(app, key, "[2a01::2]:41000")).StatusCode);
    }

    [Fact]
    [Trait("Rule", "AUTH-38")]
    public async Task AUTH38_empty_list_works_from_any_address()
    {
        await using var app = await StartAsync();
        var (browser, _, keyId, key) = await BuilderKeyAsync(app);
        (await SetAsync(browser, keyId, ["80.244.7.0/24"])).EnsureSuccessStatusCode();

        var response = await SetAsync(browser, keyId, []);

        Assert.Empty(Addresses(await JsonAsync(response)));
        Assert.Equal(HttpStatusCode.OK, (await CallAsync(app, key, "5.6.7.8:41000")).StatusCode);
    }
}
