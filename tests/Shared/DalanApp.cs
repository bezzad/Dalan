using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Host;
using Dalan.Identity.Data;
using Dalan.Identity.SmsLogin;
using Dalan.Identity.Users;
using Dalan.Notification.Contracts;
using Microsoft.AspNetCore.Hosting;
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

    /// <summary>Waits until the start-up work (migrations, queue reload) is done.</summary>
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
        foreach (var (key, value) in settings ?? new Dictionary<string, string>())
            builder.UseSetting(key, value);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<TimeProvider>(Time);
            services.AddSingleton<ILoggerProvider>(Logs);
            if (!RealSms) services.AddSingleton<ISmsSender>(Sms);
        });
        configure?.Invoke(builder);
    }
}
