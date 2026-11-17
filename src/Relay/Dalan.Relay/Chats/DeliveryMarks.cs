using Dalan.Relay.ClientApi;
using Dalan.Relay.Data;
using Dalan.Relay.Live;
using Dalan.Relay.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Dalan.Relay.Chats;

/// <summary>
/// Writes whether user messages reached the bot. Each message leaves the queue once, so each id is written once.
/// Ids of button presses match no row: a press is not stored (REL-21).
/// </summary>
public sealed class DeliveryMarks(IDbContextFactory<RelayDb> dbFactory, OutboxWorker outbox)
{
    /// <summary>Q-03. A quiet update: no new version, no event (REL-31).</summary>
    public async Task MarkDeliveredAsync(IReadOnlyCollection<long> messageIds)
    {
        if (messageIds.Count == 0) return;

        await using var db = await dbFactory.CreateDbContextAsync();
        await db.Messages
            .Where(m => messageIds.Contains(m.Id))
            .ExecuteUpdateAsync(set => set.SetProperty(m => m.Delivery, DeliveryState.Delivered));
    }

    /// <summary>
    /// Q-02, Q-04, Q-07. REL-31: each message gets the next version of its chat and an event, so the user sees the mark.
    /// The chats are locked in id order, so two of these never wait for each other.
    /// </summary>
    public async Task MarkNotDeliveredAsync(IReadOnlyCollection<long> messageIds)
    {
        if (messageIds.Count == 0) return;

        await using var db = await dbFactory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var messages = await db.Messages
            .Where(m => messageIds.Contains(m.Id) && m.Delivery == DeliveryState.Pending)
            .OrderBy(m => m.BotId).ThenBy(m => m.UserId).ThenBy(m => m.Id)
            .ToListAsync();
        if (messages.Count == 0) return;

        foreach (var message in messages)
        {
            message.Delivery = DeliveryState.NotDelivered;
            message.Version = await ChatService.NextVersionAsync(db, message.BotId, message.UserId, CancellationToken.None);
            db.Outbox.Add(OutboxEvent.Of(new MessageChanged(message.BotId, message.UserId, ChatMessageJson.Of(message))));
        }

        // REL-30: the bot never saw these files, so nobody needs them.
        var files = messages.Where(m => m.Content.File is not null).Select(m => m.Content.File!.Id).ToList();
        if (files.Count > 0)
            await db.Files.Where(f => files.Contains(f.Id)).ExecuteUpdateAsync(set => set.SetProperty(f => f.ExpiresAt, f => f.CreatedAt));

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        outbox.Wake();
    }
}
