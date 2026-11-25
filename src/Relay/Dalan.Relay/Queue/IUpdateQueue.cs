using System.Globalization;
using Dalan.Identity.Contracts;
using Dalan.Relay.Chats;
using Dalan.Relay.Contracts;

namespace Dalan.Relay.Queue;

/// <summary>
/// One update waiting for a bot: a user message (its id is the id of the stored message),
/// a press on a button of <see cref="Message"/> (REL-21) when <see cref="CallbackData"/> is set,
/// or a moved chat (REL-56) when <see cref="Moved"/> is set.
/// </summary>
public sealed record QueuedUpdate(
    long Id, UserProfile From, DateTimeOffset Date, BotMessage? Message, string? CallbackData = null, BotChatMoved? Moved = null)
{
    public static QueuedUpdate Of(Message message, UserProfile sender) => new(message.Id, sender, message.CreatedAt, message.ToBot());

    /// <summary>REL-56: <paramref name="into"/> is the kept user; the id comes from the message ids, so it sorts with them.</summary>
    public static QueuedUpdate ChatMoved(long id, UserProfile into, long fromUserId, DateTimeOffset date) =>
        new(id, into, date, null, Moved: new BotChatMoved(fromUserId, into.Id, date));

    public BotUpdate ToBot()
    {
        var from = new BotUser(From.Id, From.DisplayName, From.Username);
        if (Moved is not null) return new BotUpdate(Id, from, null, null, Moved);
        return CallbackData is null
            ? new BotUpdate(Id, from, Message, null)
            : new BotUpdate(Id, from, null, new BotCallback(Id.ToString(CultureInfo.InvariantCulture), Message!, CallbackData));
    }
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
