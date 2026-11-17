using System.Threading.Channels;
using Dalan.Relay.ClientApi;

namespace Dalan.Relay.Live;

/// <summary>Something a user's open streams must hear about.</summary>
public abstract record LiveEvent(long BotId, long UserId);

/// <summary>
/// LIVE-02: a message was saved or changed. It carries the whole message, so no reader asks the database.
/// <paramref name="New"/> is true only when the message was just saved (REL-34 pushes those).
/// </summary>
public sealed record MessageChanged(long BotId, long UserId, ChatMessageJson Message, bool New = false) : LiveEvent(BotId, UserId);

/// <summary>LIVE-10: the user read a chat. Not stored.</summary>
public sealed record ChatRead(long BotId, long UserId, int Unread) : LiveEvent(BotId, UserId);

/// <summary>LIVE-09: the bot's answer to a button press. Not stored.</summary>
public sealed record CallbackAnswered(long BotId, long UserId, string CallbackId, string? Text, bool ShowAlert)
    : LiveEvent(BotId, UserId);

/// <summary>One open live stream of a user. Dispose it when the stream ends.</summary>
public interface IMessageSubscription : IDisposable
{
    /// <summary>The events for this stream. It completes when the stream must end (replaced, or too slow).</summary>
    ChannelReader<LiveEvent> Events { get; }
}

/// <summary>
/// Passes events to the open streams of their user. In memory in version 1; this interface is the seam
/// for a shared notifier when Dalan runs on more than one server (docs/ARCHITECTURE.md section 8).
/// </summary>
public interface IMessageNotifier
{
    void Publish(LiveEvent liveEvent);

    IMessageSubscription Subscribe(long userId);
}
