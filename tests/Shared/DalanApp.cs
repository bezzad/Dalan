using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Host;
using Dalan.Identity.Data;
using Dalan.Identity.SmsLogin;
using Dalan.Identity.Users;
using Microsoft.AspNetCore.Builder;
using Dalan.Notification.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Dalan.Testing;

/// <summary>The real server in the test process, on its own database, with a clock the test controls.</summary>
public sealed class DalanApp(
    string connectionString,
    IReadOnlyDictionary<string, string>? settings = null,
    Action<IWebHostBuilder>? configure = null) : WebApplicationFactory<Program>
{
    private int _users;

    public string ConnectionString => connectionString;

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero));

    /// <summary>The SMS that the server "sent". Not used when <see cref="RealSms"/> is set.</summary>
    public FakeSmsSender Sms { get; } = new();

    /// <summary>True keeps the sender that the settings choose (for the tests of the senders themselves).</summary>
    public bool RealSms { get; init; }

    public LogCollector Logs { get; } = new();

    /// <summary>The folder of this server's files (Relay:FileFolder): a new temporary one, so tests never share files.</summary>
    public string FileFolder { get; } = Path.Combine(Path.GetTempPath(), "dalan-tests", Guid.NewGuid().ToString("N"));

    /// <summary>Waits until the start-up work (migrations, queue reload) is done.</summary>
    /// <summary>The access log rows as "actor id-or-phone address port", oldest first. Port is empty when unknown.</summary>
    public async Task<string[]> AccessLogAsync()
    {
        var rows = await PostgresServer.ScalarAsync<string[]>(connectionString,
            """
            SELECT coalesce(array_agg(actor || ' ' || coalesce(phone, actor_id::text) || ' ' || host(address) || ' ' || coalesce(port::text, '') ORDER BY id), '{}')
            FROM admin.access_log
            """);
        return [.. rows.Select(row => row.TrimEnd())];
    }

    public Task ReadyAsync() =>
        Services.GetRequiredService<Startup>().Done.WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);

    public async Task<long> AddUserAsync(string displayName = "Sara", string? username = null, string? phone = null)
    {
        var user = new User
        {
            Phone = phone ?? $"+98912000{Interlocked.Increment(ref _users):D4}",
            DisplayName = displayName,
            Username = username,
            CreatedAt = Time.GetUtcNow(),
        };

        await using var db = await Services.GetRequiredService<IDbContextFactory<IdentityDb>>().CreateDbContextAsync();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    /// <summary>A browser: it keeps cookies and talks HTTPS, so the Secure session cookie is sent back.</summary>
    public HttpClient Browser(string? ip = null)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        if (ip is not null) client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
        return client;
    }

    /// <summary>Logs a person in with the development login and returns their browser and user id.</summary>
    public async Task<(HttpClient Browser, long UserId)> LoginAsync(string name = "Sara", string? phone = null)
    {
        var browser = Browser();
        phone ??= $"0912111{Interlocked.Increment(ref _users):D4}";
        var response = await browser.PostAsJsonAsync("/api/dev/login", new { phone, name }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var user = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return (browser, user.GetProperty("id").GetInt64());
    }

    /// <summary>
    /// Makes the server see this client's calls as a connection from <paramref name="endPoint"/> ("address:port").
    /// The in-process test server has no real connection, so a test names the one it wants.
    /// </summary>
    public static HttpClient From(HttpClient client, string endPoint)
    {
        client.DefaultRequestHeaders.Add(SocketHeader, endPoint);
        return client;
    }

    private const string SocketHeader = "Test-Socket";

    private sealed class SocketFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((http, nextMiddleware) =>
            {
                if (http.Request.Headers.Remove(SocketHeader, out var value))
                {
                    var endPoint = IPEndPoint.Parse(value.ToString());
                    http.Connection.RemoteIpAddress = endPoint.Address;
                    http.Connection.RemotePort = endPoint.Port;
                }

                return nextMiddleware(http);
            });
            next(app);
        };
    }

    /// <summary>
    /// On stop, Kestrel lets running calls finish before the services are disposed (OPS-03). The in-process test server
    /// does not wait, so a call that ends because the server stops (Q-09) could find its services gone.
    /// This makes the test server wait like Kestrel.
    /// </summary>
    private sealed class RequestDrain : IStartupFilter, Microsoft.Extensions.Hosting.IHostedService
    {
        private int _running;

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (http, nextMiddleware) =>
            {
                Interlocked.Increment(ref _running);
                try
                {
                    await nextMiddleware(http);
                }
                finally
                {
                    Interlocked.Decrement(ref _running);
                }
            });
            next(app);
        };

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (Volatile.Read(ref _running) > 0 && !limit.IsCancellationRequested)
                await Task.Delay(10, CancellationToken.None);
        }
    }

    /// <summary>Does the proof of work and asks for an SMS code, like the login screen (SEC-06, AUTH-11).</summary>
    public static async Task<HttpResponseMessage> AskCodeAsync(HttpClient browser, string phone)
    {
        var cancel = TestContext.Current.CancellationToken;
        var answer = await browser.PostAsJsonAsync("/api/auth/challenge", new { phone }, cancel);
        answer.EnsureSuccessStatusCode();
        var json = await answer.Content.ReadFromJsonAsync<JsonElement>(cancel);
        var challenge = json.GetProperty("challenge").GetString()!;
        var nonce = Solve(challenge, json.GetProperty("difficulty").GetInt32());
        return await browser.PostAsJsonAsync("/api/auth/sms", new { phone, challenge, nonce }, cancel);
    }

    /// <summary>Finds a nonce for a challenge by trying 0, 1, 2, ...</summary>
    public static string Solve(string challenge, int difficulty)
    {
        for (var number = 0; ; number++)
        {
            var nonce = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (Proof.IsSolved(challenge, nonce, difficulty)) return nonce;
        }
    }

    /// <summary>Logs a person in with a real SMS code and returns their browser. The number is new unless a user has it.</summary>
    public async Task<HttpClient> LoginBySmsAsync(string phone, string name = "Sara")
    {
        var browser = Browser();
        (await AskCodeAsync(browser, phone)).EnsureSuccessStatusCode();
        var response = await browser.PostAsJsonAsync(
            "/api/auth/sms/verify", new { phone, code = Sms.Sent[^1].Code, name }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return browser;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (Directory.Exists(FileFolder)) Directory.Delete(FileFolder, recursive: true);
        GC.SuppressFinalize(this);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Dalan", connectionString);
        // Tests run in the Development environment. These two do not depend on appsettings.Development.json.
        builder.UseSetting("Identity:DevLogin", "true");
        builder.UseSetting("Identity:AccessTokenMinutes", "15");
        // Small work, so tests are fast. The real values are in appsettings.json.
        builder.UseSetting("Identity:PowDifficultyBits", "4");
        builder.UseSetting("Identity:PasswordMemoryKb", "64");
        builder.UseSetting("Identity:PasswordIterations", "1");
        builder.UseSetting("Notification:SmsProvider", "");
        // Tests run the expiry sweep themselves, so it never runs in the background at a moment they do not choose.
        builder.UseSetting("Relay:ExpirySweepSeconds", "86400");
        builder.UseSetting("Relay:FileSweepMinutes", "1440");
        builder.UseSetting("Relay:FileFolder", FileFolder);
        foreach (var (key, value) in settings ?? new Dictionary<string, string>())
            builder.UseSetting(key, value);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<TimeProvider>(Time);
            services.AddSingleton<IStartupFilter, SocketFilter>();
            services.AddSingleton<RequestDrain>();
            services.AddSingleton<IStartupFilter>(provider => provider.GetRequiredService<RequestDrain>());
            services.AddHostedService(provider => provider.GetRequiredService<RequestDrain>());
            services.AddSingleton<ILoggerProvider>(Logs);
            if (!RealSms) services.AddSingleton<ISmsSender>(Sms);
        });
        configure?.Invoke(builder);
    }
}
