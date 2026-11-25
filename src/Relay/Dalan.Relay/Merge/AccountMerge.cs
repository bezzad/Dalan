using Dalan.Identity.Contracts;
using Dalan.Notification.Contracts;
using Dalan.Relay.Chats;
using Dalan.Relay.Data;
using Dalan.Relay.Live;
using Dalan.Relay.Queue;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dalan.Relay.Merge;

/// <summary>
/// What Relay does for an account merge: counts what a user holds (AUTH-32), moves and joins the chats (REL-55),
/// and tells each bot where its chat went (REL-56). No bot changes owner.
/// </summary>
public sealed class AccountMerge(
    IDbContextFactory<RelayDb> dbFactory,
    IUpdateQueue queue,
    IUserDirectory users,
    IMessageNotifier notifier,
    IWebPush push,
    IOptionsMonitor<RelayOptions> options,
    TimeProvider time) : IUserHoldings, IUserMergeListener
{
    public async Task<UserHoldings> CountAsync(long userId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var bots = await db.Bots.CountAsync(b => b.OwnerUserId == userId && b.DeletedAt == null, cancellationToken);
        var chats = await db.Memberships.CountAsync(m => m.UserId == userId, cancellationToken);
        return new UserHoldings(Bots: bots, Chats: chats);
    }

    /// <summary>REL-55: safe to run twice: the second run finds no chat of the gone user.</summary>
    public async Task UserMergedAsync(long fromUserId, long intoUserId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        List<long> moved;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            moved = await db.Memberships.Where(m => m.UserId == fromUserId).Select(m => m.BotId).ToListAsync(cancellationToken);
            foreach (var botId in moved) await MoveChatAsync(db, botId, fromUserId, intoUserId, cancellationToken);

            await db.Files.Where(f => f.UserId == fromUserId)
                .ExecuteUpdateAsync(set => set.SetProperty(f => f.UserId, intoUserId), cancellationToken);
            // A bot made by the gone account in the moment between the product check and the merge (AUTH-32) keeps an owner.
            await db.Bots.Where(b => b.OwnerUserId == fromUserId)
                .ExecuteUpdateAsync(set => set.SetProperty(b => b.OwnerUserId, intoUserId), cancellationToken);

            foreach (var botId in moved)
            {
                await db.Database.ExecuteSqlAsync($"""
                    INSERT INTO relay.moved_chats (bot_id, from_user_id, to_user_id, moved_at)
                    VALUES ({botId}, {fromUserId}, {intoUserId}, {now})
                    ON CONFLICT (bot_id, from_user_id) DO UPDATE SET to_user_id = excluded.to_user_id, moved_at = excluded.moved_at
                    """, cancellationToken);
            }

            // A chat that moved into the gone user earlier now points at the kept one.
            await db.Database.ExecuteSqlAsync(
                $"UPDATE relay.moved_chats SET to_user_id = {intoUserId} WHERE to_user_id = {fromUserId}", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await push.MoveAsync(fromUserId, intoUserId, cancellationToken);
        if (moved.Count == 0) return;

        notifier.EndStreams(fromUserId);
        notifier.Publish(new ChatsReset(intoUserId));
        if (await users.FindAsync(intoUserId, cancellationToken) is not { } into) return;
        foreach (var botId in moved)
            queue.Enqueue(botId, QueuedUpdate.ChatMoved(await NextUpdateIdAsync(cancellationToken), into, fromUserId, now));
    }

    /// <summary>REL-56: the user id a moved chat has now, for Relay:MovedChatDays after the move.</summary>
    public async Task<long?> MovedToAsync(long botId, long userId, CancellationToken cancellationToken)
    {
        var since = time.GetUtcNow().AddDays(-options.CurrentValue.MovedChatDays);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var to = await db.Database.SqlQuery<long>($"""
            SELECT to_user_id AS "Value" FROM relay.moved_chats
            WHERE bot_id = {botId} AND from_user_id = {userId} AND moved_at > {since}
            """).ToListAsync(cancellationToken);
        return to.Count == 0 ? null : to[0];
    }

    /// <summary>
    /// One chat of the gone user. Alone, it moves with its ids. Next to a chat of the kept user, both become one chat
    /// in time order (the kept user first on a tie, then the old id), with ids after the largest old id of both,
    /// so an old id never points to another message.
    /// </summary>
    private static async Task MoveChatAsync(RelayDb db, long botId, long fromUserId, long intoUserId, CancellationToken cancellationToken)
    {
        var gone = await db.Memberships.SingleAsync(m => m.BotId == botId && m.UserId == fromUserId, cancellationToken);
        var kept = await db.Memberships.SingleOrDefaultAsync(m => m.BotId == botId && m.UserId == intoUserId, cancellationToken);
        var goneMessages = await db.Messages.Where(m => m.BotId == botId && m.UserId == fromUserId).ToListAsync(cancellationToken);

        if (kept is null)
        {
            db.Memberships.Add(new Membership
            {
                BotId = botId,
                UserId = intoUserId,
                State = gone.State,
                StartedAt = gone.StartedAt,
                LastSeq = gone.LastSeq,
                LastVersion = gone.LastVersion,
                LastReadSeq = gone.LastReadSeq,
            });
            foreach (var message in goneMessages) message.UserId = intoUserId;
        }
        else
        {
            var keptMessages = await db.Messages.Where(m => m.BotId == botId && m.UserId == intoUserId).ToListAsync(cancellationToken);
            Join(kept, gone, keptMessages, goneMessages, intoUserId);
        }

        db.Memberships.Remove(gone);
        await db.SaveChangesAsync(cancellationToken);
    }

    internal static void Join(Membership kept, Membership gone, List<Message> keptMessages, List<Message> goneMessages, long intoUserId)
    {
        var seqBase = Math.Max(kept.LastSeq, gone.LastSeq);
        var versionBase = Math.Max(kept.LastVersion, gone.LastVersion);
        var order = keptMessages.Select(m => (Message: m, Kept: true))
            .Concat(goneMessages.Select(m => (Message: m, Kept: false)))
            .OrderBy(x => x.Message.CreatedAt).ThenBy(x => x.Kept ? 0 : 1).ThenBy(x => x.Message.Seq)
            .ToList();

        var keptIds = new Dictionary<long, long>();
        var goneIds = new Dictionary<long, long>();
        for (var i = 0; i < order.Count; i++) (order[i].Kept ? keptIds : goneIds)[order[i].Message.Seq] = seqBase + i + 1;

        var keptHasPin = keptMessages.Exists(m => m.Pinned);
        for (var i = 0; i < order.Count; i++)
        {
            var (message, isKept) = order[i];
            var ids = isKept ? keptIds : goneIds;
            message.UserId = intoUserId;
            message.Seq = ids[message.Seq];
            message.Version = versionBase + i + 1;
            if (message.Content.ReplyTo is { } reply && ids.TryGetValue(reply.Seq, out var replyId)) reply.Seq = replyId;
            // REL-25: a chat has at most one pinned message; the kept user's pin stays.
            if (!isKept && keptHasPin) message.Pinned = false;
        }

        kept.LastSeq = seqBase + order.Count;
        kept.LastVersion = versionBase + order.Count;
        kept.LastReadSeq = keptIds.Where(p => p.Key <= kept.LastReadSeq).Select(p => p.Value).DefaultIfEmpty(0).Max();
        if (gone.State == MembershipState.Active) kept.State = MembershipState.Active;
        if (gone.StartedAt < kept.StartedAt) kept.StartedAt = gone.StartedAt;
    }

    /// <summary>REL-56: an update id from the message ids, so it never meets the id of a message and sorts with them.</summary>
    private async Task<long> NextUpdateIdAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return (await db.Database.SqlQuery<long>(
            $"""SELECT nextval(pg_get_serial_sequence('relay.messages', 'id')) AS "Value" """).ToListAsync(cancellationToken))[0];
    }
}
