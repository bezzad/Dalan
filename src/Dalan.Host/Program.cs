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

builder.Services
    .AddIdentityService()
    .AddRelayService()
    .AddNotificationService()
    .AddAdminService();

var app = builder.Build();

app.MapProbes();

app.MapIdentityService();
app.MapRelayService();
app.MapNotificationService();
app.MapAdminService();
app.MapTelegramAdapter();

app.Run();

public partial class Program;
