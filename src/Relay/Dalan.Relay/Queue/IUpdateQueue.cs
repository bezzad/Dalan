using Dalan.Identity.Contracts;
using Dalan.Relay.Chats;

namespace Dalan.Relay.Queue;

/// <summary>One user message waiting for a bot. Its id is the id of the stored message.</summary>
public sealed record QueuedUpdate(long Id, UserProfile From, long Seq, string Text, DateTimeOffset Date)
{
    public static QueuedUpdate Of(Message message, UserProfile sender) =>
        new(message.Id, sender, message.Seq, message.Content.Text, message.CreatedAt);
}

/// <summary>
/// The answer to one read of a bot's queue.
/// <paramref name="Delivered"/> and <paramref name="NotDelivered"/> are messages that left the queue in this read.
/// <paramref name="Arrival"/> completes when the next update is put in this bot's queue.
/// </summary>
public sealed record Batch(
    IReadOnlyList<QueuedUpdate> Updates,
    IReadOnlyList<long> Delivered,
    IReadOnlyList<long> NotDelivered,
    Task Arrival);

/// <summary>
/// The updates that wait for each bot. In memory in version 1; this interface is the seam
/// for a shared queue when Dalan runs on more than one server (docs/ARCHITECTURE.md section 8).
/// </summary>
public interface IUpdateQueue
{
    /// <summary>Adds an update. Returns the messages it replaced (Q-04).</summary>
    IReadOnlyList<long> Enqueue(long botId, QueuedUpdate update);

    /// <summary>Confirms what the bot has seen, drops what expired, and hands out the next updates.</summary>
    Batch Take(long botId, long offset, int limit);

    /// <summary>Removes, for every bot, the expired updates that were never handed out. Returns their messages.</summary>
    IReadOnlyList<long> RemoveExpired();

    /// <summary>Starts a poll for a bot and ends the one before it (Q-05). The token is cancelled when a newer poll starts.</summary>
    CancellationToken BeginPoll(long botId);
}
