using Dalan.Notification.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dalan.Notification.Inbox;

/// <summary>NTF-12: removes notices older than Notification:InboxDays, every Notification:InboxSweepMinutes.</summary>
public sealed partial class InboxSweep(
    IDbContextFactory<NotificationDb> dbFactory,
    IOptionsMonitor<NotificationOptions> options,
    TimeProvider time,
    ILogger<InboxSweep> logger) : BackgroundService
{
    public async Task<int> SweepOnceAsync(CancellationToken cancellationToken)
    {
        var oldest = time.GetUtcNow().AddDays(-options.CurrentValue.InboxDays);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Inbox.Where(n => n.CreatedAt < oldest).ExecuteDeleteAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(options.CurrentValue.InboxSweepMinutes), time, stoppingToken);
                try
                {
                    await SweepOnceAsync(stoppingToken);
                }
#pragma warning disable CA1031 // The next round tries again.
                catch (Exception exception) when (exception is not OperationCanceledException)
#pragma warning restore CA1031
                {
                    LogSweepFailed(exception.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    [LoggerMessage(LogLevel.Warning, "Removing old inbox notices failed ({Error}); the next round tries again.")]
    private partial void LogSweepFailed(string error);
}
