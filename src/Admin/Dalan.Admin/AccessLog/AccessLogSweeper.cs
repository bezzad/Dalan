using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dalan.Admin.AccessLog;

/// <summary>ADM-03: runs the access log sweep every <c>Admin:AccessLogSweepMinutes</c>.</summary>
public sealed partial class AccessLogSweeper(
    AccessLogService accessLog, IOptionsMonitor<AdminOptions> options, TimeProvider time, ILogger<AccessLogSweeper> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(options.CurrentValue.AccessLogSweepMinutes), time, stoppingToken);
                await accessLog.SweepOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
#pragma warning disable CA1031 // One failed sweep (database down) must not stop the next one.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                LogSweepFailed(exception);
            }
        }
    }

    [LoggerMessage(LogLevel.Error, "Access log sweep failed; trying again at the next sweep.")]
    private partial void LogSweepFailed(Exception exception);
}
