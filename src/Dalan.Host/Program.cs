using Dalan.Admin;
using Dalan.Admin.Settings;
using Dalan.Host;
using Dalan.Identity;
using Dalan.Notification;
using Dalan.Relay;
using Dalan.Sites;
using Dalan.Telegram;

var builder = WebApplication.CreateBuilder(args);

// ADM-07: the values the admin set win over the file and the environment, so this source comes last.
builder.Configuration.AddAdminSettings();

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
    .AddAdminService()
    .AddSitesService();

builder.Services.AddSingleton<Startup>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<Startup>());

var app = builder.Build();

app.UseClientAddress();
app.UseReadyGate();
// SEC-16: the file view host leaves first: it is under the sites domain, and it must never see Dalan's pages or session.
app.UseFileView(view => view.UseRateLimiter());
// SITE-08: a site host leaves here, before Dalan's own pages and security headers; the per-address limit still holds (SEC-02).
app.UseSites(site => site.UseRateLimiter());
app.UseSecurityHeaders();
// SITE-05: the agent's WebSocket.
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(builder.Configuration.GetValue<int>("Sites:WebSocketPingSeconds")),
});
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
app.MapSitesService();
app.MapTelegramAdapter();

app.Run();

public partial class Program;
