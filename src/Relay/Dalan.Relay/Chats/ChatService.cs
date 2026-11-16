using Dalan.Identity.Contracts;
using Dalan.Relay.Data;
using Dalan.Relay.Queue;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dalan.Relay.Chats;

public enum ChatError
{
    None,
    UnknownUser,
    TextEmpty,
    TextTooLong,
    ChatNotActive,
}

/// <summary>Saves the messages of a chat. Both doors of Relay (users and bots) end here.</summary>
public sealed class ChatService(
    IDbContextFactory<RelayDb> dbFactory,
    IUserDirectory users,
    IUpdateQueue queue,
    IOptionsMonitor<RelayOptions> options,
    TimeProvider time)
{
    /// <summary>A user writes to a bot. The first message starts the chat (REL-05).</summary>
    public async Task<(ChatError Error, Message? Message)> ReceiveFromUserAsync(
        long botId, long userId, string text, CancellationToken cancellationToken)
    {
        if (CheckText(text) is var error and not ChatError.None) return (error, null);

        var sender = await users.FindAsync(userId, cancellationToken);
        if (sender is null) return (ChatError.UnknownUser, null);

        var now = time.GetUtcNow();
        var message = await SaveAsync(
            db => db.Database.SqlQuery<long>($"""
                INSERT INTO relay.memberships (bot_id, user_id, state, started_at, last_seq)
                VALUES ({botId}, {userId}, {(int)MembershipState.Active}, {now}, 1)
                ON CONFLICT (bot_id, user_id) DO UPDATE SET last_seq = relay.memberships.last_seq + 1
                RETURNING last_seq AS "Value"
                """),
            new Message
            {
                BotId = botId,
                UserId = userId,
                Direction = Direction.FromUser,
                Content = new MessageContent { Text = text },
                CreatedAt = now,
                Delivery = DeliveryState.Pending,
            },
            cancellationToken);

        // Q-01: the message is saved; only now may the bot see it.
        var replaced = queue.Enqueue(botId, QueuedUpdate.Of(message!, sender));
        await RelayDb.MarkAsync(dbFactory, replaced, DeliveryState.NotDelivered);
        return (ChatError.None, message);
    }

    /// <summary>A bot writes to a user who has started it (REL-08).</summary>
    public async Task<(ChatError Error, Message? Message)> SendFromBotAsync(
        long botId, long userId, string text, CancellationToken cancellationToken)
    {
        if (CheckText(text) is var error and not ChatError.None) return (error, null);

        var message = await SaveAsync(
            db => db.Database.SqlQuery<long>($"""
                UPDATE relay.memberships SET last_seq = last_seq + 1
                WHERE bot_id = {botId} AND user_id = {userId} AND state = {(int)MembershipState.Active}
                RETURNING last_seq AS "Value"
                """),
            new Message
            {
                BotId = botId,
                UserId = userId,
                Direction = Direction.FromBot,
                Content = new MessageContent { Text = text },
                CreatedAt = time.GetUtcNow(),
            },
            cancellationToken);

        return message is null ? (ChatError.ChatNotActive, null) : (ChatError.None, message);
    }

    private ChatError CheckText(string text) =>
        string.IsNullOrEmpty(text) ? ChatError.TextEmpty
        : text.Length > options.CurrentValue.MaxTextLength ? ChatError.TextTooLong
        : ChatError.None;

    /// <summary>
    /// REL-06: takes the next sequence number of the chat and inserts the message in one transaction.
    /// The lock on the membership row makes the numbers of one chat commit in order.
    /// Returns null when the chat gave no number (no active membership).
    /// </summary>
    private async Task<Message?> SaveAsync(
        Func<RelayDb, IQueryable<long>> nextSeq, Message message, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var seq = await nextSeq(db).ToListAsync(cancellationToken);
        if (seq.Count == 0) return null;

        message.Seq = seq[0];
        db.Messages.Add(message);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return message;
    }
}
