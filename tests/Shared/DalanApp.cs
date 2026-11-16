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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Dalan", connectionString);
        // Tests run the expiry sweep themselves, so it never runs in the background at a moment they do not choose.
        builder.UseSetting("Relay:ExpirySweepSeconds", "86400");
        foreach (var (key, value) in settings ?? new Dictionary<string, string>())
            builder.UseSetting(key, value);

        builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(Time));
        configure?.Invoke(builder);
    }
}
