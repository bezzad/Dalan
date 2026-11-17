using Dalan.Identity.Contracts;
using Dalan.Relay.Chats;
using Dalan.Relay.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dalan.Relay.Queue;

/// <summary>Q-07: on start, puts the user messages that still wait for a bot back in the queues.</summary>
public sealed class QueueLoader(
    IDbContextFactory<RelayDb> dbFactory,
    IUserDirectory users,
    IUpdateQueue queue,
    DeliveryMarks marks,
    IOptionsMonitor<RelayOptions> options,
    TimeProvider time)
{
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var expired = time.GetUtcNow() - options.CurrentValue.UpdateExpiry;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var waiting = await db.Messages.AsNoTracking()
            .Where(m => m.Delivery == DeliveryState.Pending)
            .OrderBy(m => m.Id)
            .ToListAsync(cancellationToken);

        var senders = new Dictionary<long, UserProfile?>();
        var dropped = waiting.Where(m => m.CreatedAt <= expired).Select(m => m.Id).ToList();
        foreach (var message in waiting.Where(m => m.CreatedAt > expired))
        {
            if (!senders.TryGetValue(message.UserId, out var sender))
                senders[message.UserId] = sender = await users.FindAsync(message.UserId, cancellationToken);

            if (sender is null)
                dropped.Add(message.Id);
            else
                dropped.AddRange(queue.Enqueue(message.BotId, QueuedUpdate.Of(message, sender)));
        }

        await marks.MarkNotDeliveredAsync(dropped);
    }
}
