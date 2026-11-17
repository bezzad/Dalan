using System.Threading.Channels;
using Dalan.Relay.Chats;

namespace Dalan.Relay.Live;

/// <summary>The event "a message was saved in a chat". It carries all a live stream needs, so no reader asks the database.</summary>
public sealed record MessageCreated(
    long BotId, long UserId, long Seq, Direction Direction, string Text, List<List<string>>? ReplyKeyboard, DateTimeOffset Date)
{
    public static MessageCreated Of(Message message) => new(
        message.BotId, message.UserId, message.Seq, message.Direction,
        message.Content.Text, message.Content.ReplyKeyboard?.ToRows(), message.CreatedAt);
}

/// <summary>One open live stream of a user. Dispose it when the stream ends.</summary>
public interface IMessageSubscription : IDisposable
{
    /// <summary>The events for this stream. It completes when the stream must end (replaced, or too slow).</summary>
    ChannelReader<MessageCreated> Events { get; }
}

/// <summary>
/// Passes new messages to the open streams of their user. In memory in version 1; this interface is the seam
/// for a shared notifier when Dalan runs on more than one server (docs/ARCHITECTURE.md section 8).
/// </summary>
public interface IMessageNotifier
{
    void Publish(MessageCreated message);

    IMessageSubscription Subscribe(long userId);
}
