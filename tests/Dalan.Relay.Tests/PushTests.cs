using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Dalan.Notification.Contracts;
using Dalan.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Relay.Tests;

/// <summary>REL-34, REL-35 and NTF-03 to NTF-09: Web Push, from the bot's message to the encrypted request.</summary>
public class PushTests(PostgresServer postgres)
{
    private const string NoDatabase = "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=2";
    private static readonly (string Public, string Private) Vapid = NewVapidKeys();
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static (string, string) NewVapidKeys()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = key.ExportParameters(includePrivateParameters: true);
        return (Base64Url.EncodeToString([0x04, .. parameters.Q.X!, .. parameters.Q.Y!]), Base64Url.EncodeToString(parameters.D));
    }

    /// <summary>A browser's push subscription: its own key pair and auth secret, so a test can decrypt what it gets.</summary>
    private sealed class Device : IDisposable
    {
        public ECDiffieHellman Key { get; } = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        public byte[] AuthSecret { get; } = RandomNumberGenerator.GetBytes(16);
        public required string Endpoint { get; init; }

        public byte[] PublicKey
        {
            get
            {
                var q = Key.ExportParameters(false).Q;
                return [0x04, .. q.X!, .. q.Y!];
            }
        }

        public object Json => new
        {
            endpoint = Endpoint,
            expirationTime = (object?)null,
            keys = new { p256dh = Base64Url.EncodeToString(PublicKey), auth = Base64Url.EncodeToString(AuthSecret) },
        };

        /// <summary>RFC 8291 and RFC 8188: what the browser does with the body of a push.</summary>
        public string Decrypt(byte[] body)
        {
            var salt = body[..16];
            var idLength = body[20];
            var serverKey = body[21..(21 + idLength)];
            var cipher = body[(21 + idLength)..];

            using var server = ECDiffieHellman.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = serverKey[1..33], Y = serverKey[33..65] },
            });
            var shared = Key.DeriveRawSecretAgreement(server.PublicKey);
            var keyInfo = Encoding.ASCII.GetBytes("WebPush: info\0").Concat(PublicKey).Concat(serverKey).ToArray();
            var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, 32, AuthSecret, keyInfo);
            var cek = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt, Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));
            var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, salt, Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));

            var plain = new byte[cipher.Length - 16];
            using var aes = new AesGcm(cek, 16);
            aes.Decrypt(nonce, cipher[..^16], cipher[^16..], plain);
            var end = Array.LastIndexOf(plain, (byte)2);
            return Encoding.UTF8.GetString(plain, 0, end);
        }

        public void Dispose() => Key.Dispose();
    }

    private sealed record Sent(string Endpoint, HttpRequestMessage Request, byte[] Body);

    /// <summary>Plays the push services: keeps each request and answers with the status the test chooses.</summary>
    private sealed class FakePushService : HttpMessageHandler
    {
        public Channel<Sent> Requests { get; } = Channel.CreateUnbounded<Sent>();
        public Func<string, HttpStatusCode> Status { get; set; } = _ => HttpStatusCode.Created;
        public bool Hangs { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var endpoint = request.RequestUri!.ToString();
            Requests.Writer.TryWrite(new Sent(endpoint, request, await request.Content!.ReadAsByteArrayAsync(cancellationToken)));
            if (Hangs) await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(Status(endpoint));
        }

        public async Task<Sent> NextAsync()
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
            limit.CancelAfter(TimeSpan.FromSeconds(30));
            return await Requests.Reader.ReadAsync(limit.Token);
        }
    }

    /// <summary>Stands in for Notification: keeps the pushes Relay passes on (REL-34).</summary>
    private sealed class FakeWebPush : IWebPush
    {
        public Channel<(long UserId, PushNote Note)> Sent { get; } = Channel.CreateUnbounded<(long, PushNote)>();
        public string? PublicKey => null;

        public Task<SubscribeResult> SubscribeAsync(long userId, PushDevice device, CancellationToken cancellationToken) =>
            Task.FromResult(SubscribeResult.PushOff);

        public Task UnsubscribeAsync(long userId, string endpoint, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task MoveAsync(long fromUserId, long intoUserId, CancellationToken cancellationToken) => Task.CompletedTask;

        public void Send(long userId, PushNote note) => Sent.Writer.TryWrite((userId, note));

        public async Task<PushNote> NextAsync()
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
            limit.CancelAfter(TimeSpan.FromSeconds(30));
            return (await Sent.Reader.ReadAsync(limit.Token)).Note;
        }
    }

    private static Dictionary<string, string> PushSettings(params (string Key, string Value)[] more)
    {
        var settings = new Dictionary<string, string>
        {
            ["Notification:Push:PublicKey"] = Vapid.Public,
            ["Notification:Push:PrivateKey"] = Vapid.Private,
            ["Notification:Push:Subject"] = "https://dalanet.ir",
            ["Notification:Push:AllowedHosts"] = "push.test,push.apple.com",
            ["Notification:Push:Workers"] = "1",
            ["Notification:Push:QueueSize"] = "100",
        };
        foreach (var (key, value) in more) settings[key] = value;
        return settings;
    }

    private static DalanApp App(string database, FakePushService service, Dictionary<string, string> settings) => new(
        database,
        settings,
        web => web.ConfigureServices(services =>
            services.ConfigureHttpClientDefaults(client => client.ConfigurePrimaryHttpMessageHandler(() => service))));

    private async Task<(DalanApp App, FakePushService Service)> StartAsync(params (string Key, string Value)[] settings)
    {
        var service = new FakePushService();
        var app = App(await postgres.NewDatabaseAsync(), service, PushSettings(settings));
        await app.ReadyAsync();
        return (app, service);
    }

    private static Task<HttpResponseMessage> SubscribeAsync(HttpClient browser, object body) =>
        browser.PostAsJsonAsync("/api/push/subscribe", body, Cancel);

    private static Task<string[]> EndpointsAsync(DalanApp app, long userId) =>
        PostgresServer.ScalarAsync<string[]>(app.ConnectionString,
            $"SELECT coalesce(array_agg(endpoint ORDER BY endpoint), '{{}}') FROM notification.push_subscriptions WHERE user_id = {userId}");

    private static IWebPush Push(DalanApp app) => app.Services.GetRequiredService<IWebPush>();

    // ---------- REL-34 ----------

    private static DalanApp FakePushApp(string database, FakeWebPush push) => new(
        database, null, web => web.ConfigureServices(services => services.AddSingleton<IWebPush>(push)));

    [Fact]
    [Trait("Rule", "REL-34")]
    public async Task REL34_new_bot_message_is_pushed_with_name_text_link_and_tag()
    {
        var push = new FakeWebPush();
        await using var app = FakePushApp(await postgres.NewDatabaseAsync(), push);
        await app.ReadyAsync();
        var (botId, token) = await app.AddBotAsync("shopbot");
        var (browser, userId) = await app.LoginAsync();
        await browser.SayAsync(botId, "/start");

        await app.BotClient(token).SendAsync(userId, "Your order is ready");

        var note = await push.NextAsync();
        Assert.Equal(new PushNote("Test bot", "Your order is ready", "/@shopbot", $"bot-{botId}"), note);
    }

    [Fact]
    [Trait("Rule", "REL-34")]
    public async Task REL34_file_without_caption_pushes_its_name()
    {
        var push = new FakeWebPush();
        await using var app = FakePushApp(await postgres.NewDatabaseAsync(), push);
        await app.ReadyAsync();
        var chat = await Chat.StartAsync(app);

        var response = await chat.BotUploadAsync("%PDF"u8.ToArray(), "menu.pdf", "document");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("menu.pdf", (await push.NextAsync()).Body);
    }

    [Fact]
    [Trait("Rule", "REL-34")]
    public async Task REL34_user_messages_edits_and_pins_are_not_pushed()
    {
        var push = new FakeWebPush();
        await using var app = FakePushApp(await postgres.NewDatabaseAsync(), push);
        await app.ReadyAsync();
        var chat = await Chat.StartAsync(app);
        var first = await chat.BotSaysAsync("first");
        Assert.Equal("first", (await push.NextAsync()).Body);

        await chat.Browser.SayAsync(chat.BotId, "hello");
        await chat.EditAsync(first, new { text = "edited" });
        await chat.PinAsync(first);
        await chat.BotSaysAsync("last");

        // The outbox keeps the order, so anything pushed for the changes above would come before "last".
        Assert.Equal("last", (await push.NextAsync()).Body);
    }

    // ---------- REL-35 ----------

    [Fact]
    [Trait("Rule", "REL-35")]
    [Trait("Rule", "NTF-03")]
    public async Task REL35_subscribe_stores_the_device_for_the_user()
    {
        var (app, _) = await StartAsync();
        await using var _1 = app;
        var (browser, userId) = await app.LoginAsync();
        using var device = new Device { Endpoint = "https://push.test/a" };

        var key = (await browser.JsonAsync("/api/push")).GetProperty("key").GetString();
        var response = await SubscribeAsync(browser, device.Json);

        Assert.Equal(Vapid.Public, key);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(["https://push.test/a"], await EndpointsAsync(app, userId));
    }

    [Fact]
    [Trait("Rule", "REL-35")]
    [Trait("Rule", "NTF-05")]
    public async Task REL35_push_off_without_keys()
    {
        await using var app = await RelayApp.StartAsync(postgres);
        var (browser, _) = await app.LoginAsync();
        using var device = new Device { Endpoint = "https://fcm.googleapis.com/x" };

        var key = (await browser.JsonAsync("/api/push")).GetProperty("key");
        var response = await SubscribeAsync(browser, device.Json);

        Assert.Equal(JsonValueKind.Null, key.ValueKind);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("push_off", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "REL-35")]
    public async Task REL35_only_the_owner_can_unsubscribe()
    {
        var (app, _) = await StartAsync();
        await using var _1 = app;
        var (owner, ownerId) = await app.LoginAsync();
        var (other, _) = await app.LoginAsync("Ali");
        using var device = new Device { Endpoint = "https://push.test/a" };
        await SubscribeAsync(owner, device.Json);

        var foreign = await other.PostAsJsonAsync("/api/push/unsubscribe", new { endpoint = device.Endpoint }, Cancel);
        var stays = await EndpointsAsync(app, ownerId);
        var own = await owner.PostAsJsonAsync("/api/push/unsubscribe", new { endpoint = device.Endpoint }, Cancel);

        Assert.Equal(HttpStatusCode.NoContent, foreign.StatusCode);
        Assert.Single(stays);
        Assert.Equal(HttpStatusCode.NoContent, own.StatusCode);
        Assert.Empty(await EndpointsAsync(app, ownerId));
    }

    [Fact]
    [Trait("Rule", "REL-35")]
    public async Task REL35_push_door_needs_a_session()
    {
        await using var app = await RelayApp.StartAsync(postgres);

        var response = await app.Browser().GetAsync("/api/push", Cancel);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- NTF-03 and NTF-04 ----------

    [Fact]
    [Trait("Rule", "NTF-03")]
    public async Task NTF03_same_endpoint_moves_to_the_new_user()
    {
        var (app, _) = await StartAsync();
        await using var _1 = app;
        var (sara, saraId) = await app.LoginAsync();
        var (ali, aliId) = await app.LoginAsync("Ali");
        using var device = new Device { Endpoint = "https://push.test/shared" };

        await SubscribeAsync(sara, device.Json);
        await SubscribeAsync(ali, device.Json);

        Assert.Empty(await EndpointsAsync(app, saraId));
        Assert.Equal([device.Endpoint], await EndpointsAsync(app, aliId));
    }

    [Fact]
    [Trait("Rule", "NTF-03")]
    public async Task NTF03_oldest_goes_over_the_limit()
    {
        var (app, _) = await StartAsync(("Notification:Push:MaxSubscriptionsPerUser", "2"));
        await using var _1 = app;
        var (browser, userId) = await app.LoginAsync();

        foreach (var name in new[] { "a", "b", "c" })
        {
            using var device = new Device { Endpoint = $"https://push.test/{name}" };
            await SubscribeAsync(browser, device.Json);
            app.Time.Advance(TimeSpan.FromMinutes(1));
        }

        Assert.Equal(["https://push.test/b", "https://push.test/c"], await EndpointsAsync(app, userId));
    }

    [Theory]
    [Trait("Rule", "NTF-04")]
    [InlineData("https://evil.example/push/1")]
    [InlineData("http://push.test/x")]
    [InlineData("https://push.test:8443/x")]
    [InlineData("https://notpush.test/x")]
    [InlineData("https://user@push.test/x")]
    public async Task NTF04_unknown_push_service_is_refused(string endpoint)
    {
        var (app, _) = await StartAsync();
        await using var _1 = app;
        var (browser, userId) = await app.LoginAsync();
        using var device = new Device { Endpoint = endpoint };

        var response = await SubscribeAsync(browser, device.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("bad_subscription", await response.ErrorCodeAsync());
        Assert.Empty(await EndpointsAsync(app, userId));
    }

    [Fact]
    [Trait("Rule", "NTF-04")]
    public async Task NTF04_subdomain_of_a_known_host_is_stored()
    {
        var (app, _) = await StartAsync();
        await using var _1 = app;
        var (browser, _) = await app.LoginAsync();
        using var device = new Device { Endpoint = "https://web.push.apple.com/abc" };

        Assert.Equal(HttpStatusCode.NoContent, (await SubscribeAsync(browser, device.Json)).StatusCode);
    }

    [Fact]
    [Trait("Rule", "NTF-04")]
    public async Task NTF04_keys_of_the_wrong_size_are_refused()
    {
        var (app, _) = await StartAsync();
        await using var _1 = app;
        var (browser, _) = await app.LoginAsync();
        using var device = new Device { Endpoint = "https://push.test/a" };
        var p256dh = Base64Url.EncodeToString(device.PublicKey);

        var shortAuth = await SubscribeAsync(browser, new { endpoint = device.Endpoint, keys = new { p256dh, auth = "AAAAAAA" } });
        var noKeys = await SubscribeAsync(browser, new { endpoint = device.Endpoint });

        Assert.Equal(HttpStatusCode.BadRequest, shortAuth.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noKeys.StatusCode);
    }

    // ---------- NTF-05 ----------

    [Theory]
    [Trait("Rule", "NTF-05")]
    [InlineData("", "private")]
    [InlineData("public", "")]
    [InlineData("bad", "bad")]
    public async Task NTF05_wrong_keys_stop_the_start(string publicKey, string privateKey)
    {
        var settings = PushSettings(
            ("Notification:Push:PublicKey", publicKey == "public" ? Vapid.Public : publicKey),
            ("Notification:Push:PrivateKey", privateKey == "private" ? Vapid.Private : privateKey));
        await using var app = App(NoDatabase, new FakePushService(), settings);

        var error = Assert.Throws<InvalidOperationException>(() => app.CreateClient());

        Assert.Contains("Notification:Push", error.Message, StringComparison.Ordinal);
    }

    // ---------- NTF-06 ----------

    [Fact]
    [Trait("Rule", "NTF-06")]
    public async Task NTF06_each_device_gets_one_encrypted_signed_post()
    {
        var (app, service) = await StartAsync(("Notification:Push:TtlSeconds", "3600"));
        await using var _1 = app;
        var (browser, userId) = await app.LoginAsync();
        using var phone = new Device { Endpoint = "https://push.test/phone" };
        using var laptop = new Device { Endpoint = "https://push.test/laptop" };
        await SubscribeAsync(browser, phone.Json);
        await SubscribeAsync(browser, laptop.Json);

        Push(app).Send(userId, new PushNote("Shop", "Your order is ready", "/@shopbot", "bot-7"));

        var devices = new[] { phone, laptop }.ToDictionary(d => d.Endpoint);
        for (var i = 0; i < 2; i++)
        {
            var sent = await service.NextAsync();
            var device = devices[sent.Endpoint];
            devices.Remove(sent.Endpoint);
            Assert.Equal(HttpMethod.Post, sent.Request.Method);
            Assert.Equal("aes128gcm", Assert.Single(sent.Request.Content!.Headers.ContentEncoding));
            Assert.StartsWith("vapid ", sent.Request.Headers.Authorization!.ToString(), StringComparison.Ordinal);
            Assert.Contains($"k={Vapid.Public}", sent.Request.Headers.Authorization!.ToString(), StringComparison.Ordinal);
            Assert.Equal("3600", Assert.Single(sent.Request.Headers.GetValues("TTL")));
            Assert.Equal("high", Assert.Single(sent.Request.Headers.GetValues("Urgency")));
            Assert.Equal("bot-7", Assert.Single(sent.Request.Headers.GetValues("Topic")));

            var payload = JsonSerializer.Deserialize<JsonElement>(device.Decrypt(sent.Body));
            Assert.Equal("Shop", payload.GetProperty("title").GetString());
            Assert.Equal("Your order is ready", payload.GetProperty("body").GetString());
            Assert.Equal("/@shopbot", payload.GetProperty("url").GetString());
            Assert.Equal("bot-7", payload.GetProperty("tag").GetString());
        }

        Assert.Empty(devices);
    }

    [Fact]
    [Trait("Rule", "NTF-06")]
    public async Task NTF06_long_body_is_cut()
    {
        var (app, service) = await StartAsync(("Notification:Push:MaxBodyLength", "10"));
        await using var _1 = app;
        var (browser, userId) = await app.LoginAsync();
        using var device = new Device { Endpoint = "https://push.test/a" };
        await SubscribeAsync(browser, device.Json);

        Push(app).Send(userId, new PushNote("Shop", new string('x', 50), "/@shopbot", "bot-7"));

        var body = JsonSerializer.Deserialize<JsonElement>(device.Decrypt((await service.NextAsync()).Body)).GetProperty("body").GetString()!;
        Assert.InRange(body.Length, 1, 10);
    }

    [Fact]
    [Trait("Rule", "NTF-06")]
    public async Task NTF06_user_with_no_device_makes_no_request()
    {
        var (app, service) = await StartAsync();
        await using var _1 = app;
        var (browser, userId) = await app.LoginAsync();
        using var device = new Device { Endpoint = "https://push.test/a" };
        await SubscribeAsync(browser, device.Json);

        Push(app).Send(userId + 1000, new PushNote("Shop", "nobody", "/@shopbot", "bot-7"));
        Push(app).Send(userId, new PushNote("Shop", "somebody", "/@shopbot", "bot-7"));

        // One worker sends in order: the first request is the second push.
        Assert.Equal(device.Endpoint, (await service.NextAsync()).Endpoint);
        Assert.False(service.Requests.Reader.TryRead(out _));
    }

    // ---------- NTF-07 ----------

    [Fact]
    [Trait("Rule", "NTF-07")]
    public async Task NTF07_full_queue_drops_the_push_without_waiting()
    {
        var (app, service) = await StartAsync(("Notification:Push:QueueSize", "1"));
        await using var _1 = app;
        service.Hangs = true;
        var (browser, userId) = await app.LoginAsync();
        using var device = new Device { Endpoint = "https://push.test/a" };
        await SubscribeAsync(browser, device.Json);
        var note = new PushNote("Shop", "hi", "/@shopbot", "bot-7");

        Push(app).Send(userId, note);
        await service.NextAsync();
        Push(app).Send(userId, note);
        Push(app).Send(userId, note);

        Assert.Contains(app.Logs.Lines, line => line.Text.Contains("push was dropped", StringComparison.Ordinal));
    }

    // ---------- NTF-08 and NTF-09 ----------

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        for (var i = 0; i < 100 && !await condition(); i++) await Task.Delay(50, Cancel);
        Assert.True(await condition());
    }

    [Fact]
    [Trait("Rule", "NTF-08")]
    public async Task NTF08_gone_subscription_is_deleted_and_others_stay()
    {
        var (app, service) = await StartAsync();
        await using var _1 = app;
        service.Status = endpoint => endpoint.EndsWith("/old", StringComparison.Ordinal) ? HttpStatusCode.Gone : HttpStatusCode.Created;
        var (browser, userId) = await app.LoginAsync();
        using var old = new Device { Endpoint = "https://push.test/old" };
        using var fresh = new Device { Endpoint = "https://push.test/new" };
        await SubscribeAsync(browser, old.Json);
        await SubscribeAsync(browser, fresh.Json);

        Push(app).Send(userId, new PushNote("Shop", "hi", "/@shopbot", "bot-7"));

        await WaitUntilAsync(async () => (await EndpointsAsync(app, userId)).Length == 1);
        Assert.Equal([fresh.Endpoint], await EndpointsAsync(app, userId));
    }

    [Fact]
    [Trait("Rule", "NTF-08")]
    [Trait("Rule", "NTF-09")]
    public async Task NTF08_other_failure_keeps_the_subscription_and_logs_no_secret()
    {
        var (app, service) = await StartAsync();
        await using var _1 = app;
        service.Status = endpoint => endpoint.EndsWith("/bad", StringComparison.Ordinal)
            ? HttpStatusCode.InternalServerError : HttpStatusCode.Created;
        var (browser, userId) = await app.LoginAsync();
        using var bad = new Device { Endpoint = "https://push.test/bad" };
        using var good = new Device { Endpoint = "https://push.test/good" };
        await SubscribeAsync(browser, bad.Json);
        await SubscribeAsync(browser, good.Json);

        Push(app).Send(userId, new PushNote("Shop", "secret order 4711", "/@shopbot", "bot-7"));

        await WaitUntilAsync(() => Task.FromResult(app.Logs.Lines.Any(line => line.Text.Contains("Push failed", StringComparison.Ordinal))));
        Assert.Equal(2, (await EndpointsAsync(app, userId)).Length);
        foreach (var secret in new[] { Vapid.Private, Base64Url.EncodeToString(bad.AuthSecret), Base64Url.EncodeToString(good.AuthSecret), "4711" })
            Assert.DoesNotContain(app.Logs.Lines, line => line.Text.Contains(secret, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Rule", "NTF-06")]
    public void NTF06_cut_never_splits_a_character()
    {
        var cut = Dalan.Notification.Push.PushWorker.Cut("ab😀cd", 4);

        Assert.Equal("ab…", cut);
    }
}
