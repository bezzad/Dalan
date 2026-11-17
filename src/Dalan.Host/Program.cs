using Dalan.Admin;
using Dalan.Host;
using Dalan.Identity;
using Dalan.Notification;
using Dalan.Relay;
using Dalan.Telegram;

var builder = WebApplication.CreateBuilder(args);

// OPS-04: one JSON object per line on stdout.
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(o =>
{
    o.TimestampFormat = "O";
    o.UseUtcTimestamp = true;
});

// OPS-03: running requests get this long to finish on stop.
builder.Services.Configure<HostOptions>(o =>
    o.ShutdownTimeout = TimeSpan.FromSeconds(builder.Configuration.GetValue<int>("Dalan:ShutdownTimeoutSeconds")));

builder.Services.AddClientAddress(builder.Configuration);
builder.Services.AddIpRateLimit(builder.Configuration);

builder.Services
    .AddIdentityService()
    .AddRelayService()
    .AddNotificationService()
    .AddAdminService();

builder.Services.AddSingleton<Startup>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<Startup>());

var app = builder.Build();

app.UseClientAddress();
app.UseSecurityHeaders();
app.UseReadyGate();
app.UseClientFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapProbes();
app.MapClientPage();

app.MapIdentityService();
app.MapRelayService();
app.MapNotificationService();
app.MapAdminService();
app.MapTelegramAdapter();

app.Run();

public partial class Program;
