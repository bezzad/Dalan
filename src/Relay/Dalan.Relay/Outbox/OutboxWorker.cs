using Dalan.Relay.Data;
using Dalan.Relay.Live;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dalan.Relay.Outbox;

/// <summary>
/// LIVE-04: reads the outbox and passes each event to the notifier, at least once.
/// A writer wakes it right after its commit; the poll is only the safety net (for example after a crash).
/// </summary>
public sealed partial class OutboxWorker(
    IDbContextFactory<RelayDb> dbFactory,
    IMessageNotifier notifier,
    IOptionsMonitor<RelayOptions> options,
    TimeProvider time,
    ILogger<OutboxWorker> logger) : BackgroundService
{
    private volatile TaskCompletionSource _wake = NewSignal();

    /// <summary>Tells the worker that a new event was committed.</summary>
    public void Wake() => _wake.TrySetResult();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _wake.Task.WaitAsync(TimeSpan.FromSeconds(options.CurrentValue.OutboxPollSeconds), time, stoppingToken);
            }
            catch (TimeoutException)
            {
                // Poll time: look anyway.
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // A new signal before reading, so a wake during the read starts one more round.
            _wake = NewSignal();
            try
            {
                while (await PassOnBatchAsync(stoppingToken) > 0)
                {
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // One failed round (database down) must not stop the worker. The events stay in the table.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                LogRoundFailed(exception);
            }
        }
    }

    /// <summary>
    /// Takes the oldest events, passes them on and deletes them, in one transaction.
    /// If passing on fails, the transaction rolls back and the events stay. SKIP LOCKED lets a second worker run beside this one.
    /// </summary>
    private async Task<int> PassOnBatchAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var batch = options.CurrentValue.OutboxBatchSize;
        var events = await db.Outbox
            .FromSql($"SELECT * FROM relay.outbox ORDER BY id LIMIT {batch} FOR UPDATE SKIP LOCKED")
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        if (events.Count == 0) return 0;

        foreach (var outboxEvent in events)
        {
            if (outboxEvent.AsMessageCreated() is { } message) notifier.Publish(message);
        }

        var ids = events.ConvertAll(e => e.Id);
        await db.Outbox.Where(e => ids.Contains(e.Id)).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return events.Count;
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [LoggerMessage(LogLevel.Error, "Passing on outbox events failed; they stay in the table for the next round.")]
    private partial void LogRoundFailed(Exception exception);
}
