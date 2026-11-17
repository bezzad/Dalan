using System.Net.Http.Json;
using System.Text.Json;
using Dalan.Host;
using Dalan.Identity.Data;
using Dalan.Identity.Users;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Dalan", connectionString);
        // Tests run in the Development environment. These two do not depend on appsettings.Development.json.
        builder.UseSetting("Identity:DevLogin", "true");
        builder.UseSetting("Identity:AccessTokenMinutes", "15");
        // Tests run the expiry sweep themselves, so it never runs in the background at a moment they do not choose.
        builder.UseSetting("Relay:ExpirySweepSeconds", "86400");
        foreach (var (key, value) in settings ?? new Dictionary<string, string>())
            builder.UseSetting(key, value);

        builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(Time));
        configure?.Invoke(builder);
    }
}
