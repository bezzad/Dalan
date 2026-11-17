using Dalan.Relay.Chats;
using Dalan.Relay.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dalan.Relay.Queue;

/// <summary>Q-02: marks unread, expired updates "not delivered" while a bot is away. Also forgets old button presses (REL-22).</summary>
public sealed partial class ExpirySweeper(
    IUpdateQueue queue,
    Buttons.ButtonPresses presses,
    IDbContextFactory<RelayDb> dbFactory,
    IOptionsMonitor<RelayOptions> options,
    TimeProvider time,
    ILogger<ExpirySweeper> logger) : BackgroundService
{
    public Task SweepOnceAsync()
    {
        presses.RemoveOld();
        return RelayDb.MarkAsync(dbFactory, queue.RemoveExpired(), DeliveryState.NotDelivered);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(options.CurrentValue.ExpirySweepSeconds), time, stoppingToken);
                await SweepOnceAsync();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
#pragma warning disable CA1031 // One failed sweep (database down) must not stop the next one. Start-up repairs the states (Q-07).
            catch (Exception exception)
#pragma warning restore CA1031
            {
                LogSweepFailed(exception);
            }
        }
    }

    [LoggerMessage(LogLevel.Error, "Expiry sweep failed; trying again at the next sweep.")]
    private partial void LogSweepFailed(Exception exception);
}
