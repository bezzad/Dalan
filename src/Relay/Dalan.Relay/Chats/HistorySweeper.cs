using Dalan.Relay.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dalan.Relay.Chats;

/// <summary>
/// REL-44: keeps each chat's newest Relay:HistoryMessages messages of at most Relay:HistoryDays days.
/// A user message that still waits for the bot stays: it expires by Q-02 first. Sequence numbers keep counting,
/// because they live in the membership row, so sync (REL-16) does not change.
/// </summary>
public sealed partial class HistorySweeper(
    IDbContextFactory<RelayDb> dbFactory,
    IOptionsMonitor<RelayOptions> options,
    TimeProvider time,
    ILogger<HistorySweeper> logger) : BackgroundService
{
    private const int BatchSize = 1000;

    public async Task SweepOnceAsync(CancellationToken cancellationToken)
    {
        var limits = options.CurrentValue;
        var cutoff = time.GetUtcNow().AddDays(-limits.HistoryDays);
        var keep = (long)limits.HistoryMessages;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Small batches, so a big first sweep does not hold one long lock.
        while (await db.Database.ExecuteSqlAsync(
            $"""
            DELETE FROM relay.messages WHERE id IN (
                SELECT m.id FROM relay.messages m
                JOIN relay.memberships c ON c.bot_id = m.bot_id AND c.user_id = m.user_id
                WHERE (m.created_at < {cutoff} OR m.seq <= c.last_seq - {keep})
                  AND m.delivery IS DISTINCT FROM {(int)DeliveryState.Pending}
                LIMIT {BatchSize})
            """, cancellationToken) == BatchSize)
        {
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(options.CurrentValue.HistorySweepMinutes), time, stoppingToken);
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

    [LoggerMessage(LogLevel.Error, "History sweep failed; trying again at the next sweep.")]
    private partial void LogSweepFailed(Exception exception);
}
