using Dalan.Relay.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dalan.Relay.Files;

/// <summary>REL-30: deletes expired files from disk and table.</summary>
public sealed partial class FileSweeper(
    IDbContextFactory<RelayDb> dbFactory,
    FileStore store,
    IOptionsMonitor<RelayOptions> options,
    TimeProvider time,
    ILogger<FileSweeper> logger) : BackgroundService
{
    public async Task SweepOnceAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var now = time.GetUtcNow();
        while (true)
        {
            var ids = await db.Files.Where(f => f.ExpiresAt <= now).Select(f => f.Id).Take(500).ToListAsync(cancellationToken);
            if (ids.Count == 0) return;

            // Disk first: a row without its file is harmless, a file without its row would stay for ever.
            foreach (var id in ids) store.Delete(id);
            await db.Files.Where(f => ids.Contains(f.Id)).ExecuteDeleteAsync(cancellationToken);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(options.CurrentValue.FileSweepMinutes), time, stoppingToken);
                await SweepOnceAsync(stoppingToken);
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

    [LoggerMessage(LogLevel.Error, "File sweep failed; trying again at the next sweep.")]
    private partial void LogSweepFailed(Exception exception);
}
