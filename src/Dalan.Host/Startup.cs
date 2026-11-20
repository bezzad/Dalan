using Dalan.Admin;
using Dalan.Identity;
using Dalan.Notification;
using Dalan.Relay;
using Dalan.Sites;

namespace Dalan.Host;

/// <summary>
/// The work that must finish before the server takes traffic: each service applies its migrations (OPS-07)
/// and Relay reloads its queues (Q-07). It runs in the background so /health answers from the first moment,
/// and it tries again while the database is down.
/// </summary>
public sealed partial class Startup(
    IServiceProvider services, IConfiguration configuration, TimeProvider time, ILogger<Startup> logger) : BackgroundService
{
    private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes when the server is ready for traffic.</summary>
    public Task Done => _done.Task;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retry = TimeSpan.FromSeconds(configuration.GetValue<int>("Dalan:StartupRetrySeconds"));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Admin first: its stored settings (ADM-07) must hold before the others start.
                await AdminService.StartAsync(services, stoppingToken);
                await IdentityService.StartAsync(services, stoppingToken);
                await NotificationService.StartAsync(services, stoppingToken);
                await RelayService.StartAsync(services, stoppingToken);
                await SitesService.StartAsync(services, stoppingToken);
                _done.SetResult();
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // Any failure here means "not ready yet": log it and try again.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                LogStartupFailed(exception, retry.TotalSeconds);
            }

            try
            {
                await Task.Delay(retry, time, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    [LoggerMessage(LogLevel.Error,
        "Start-up work failed. Is ConnectionStrings:Dalan set and the database up? Trying again in {Seconds} s.")]
    private partial void LogStartupFailed(Exception exception, double seconds);
}
