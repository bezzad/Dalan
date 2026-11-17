using Dalan.Notification.Contracts;
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
    IWebPush push,
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

        var changes = events.Select(e => e.AsMessageChanged()).OfType<MessageChanged>().ToList();
        foreach (var change in changes) notifier.Publish(change);
        await PushAsync(db, changes, cancellationToken);

        var ids = events.ConvertAll(e => e.Id);
        await db.Outbox.Where(e => ids.Contains(e.Id)).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return events.Count;
    }

    /// <summary>REL-34: a push for each new bot message. Sending only fills Notification's queue, so this is quick.</summary>
    private async Task PushAsync(RelayDb db, List<MessageChanged> changes, CancellationToken cancellationToken)
    {
        var fresh = changes.FindAll(c => c.New && c.Message.From == "bot");
        if (fresh.Count == 0) return;

        var botIds = fresh.Select(c => c.BotId).Distinct().ToList();
        var bots = await db.Bots.Where(b => botIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => new { b.Name, b.Username }, cancellationToken);
        foreach (var change in fresh)
        {
            if (!bots.TryGetValue(change.BotId, out var bot)) continue;
            var message = change.Message;
            var body = message.Text.Length > 0 ? message.Text : message.File?.Name ?? "";
            push.Send(change.UserId, new PushNote(bot.Name, body, $"/@{bot.Username}", $"bot-{change.BotId}"));
        }
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [LoggerMessage(LogLevel.Error, "Passing on outbox events failed; they stay in the table for the next round.")]
    private partial void LogRoundFailed(Exception exception);
}
