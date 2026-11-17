using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace Dalan.Relay.Live;

public sealed class InMemoryMessageNotifier(IOptionsMonitor<RelayOptions> options) : IMessageNotifier
{
    private sealed class Subscription(InMemoryMessageNotifier owner, long userId, int bufferSize) : IMessageSubscription
    {
        // Full buffer = TryWrite fails, and the stream is closed (LIVE-08).
        public Channel<LiveEvent> Channel { get; } = System.Threading.Channels.Channel.CreateBounded<LiveEvent>(bufferSize);

        public ChannelReader<LiveEvent> Events => Channel.Reader;

        public void Dispose() => owner.Remove(userId, this);
    }

    private readonly Lock _lock = new();
    private readonly Dictionary<long, List<Subscription>> _streams = [];

    public void Publish(LiveEvent liveEvent)
    {
        lock (_lock)
        {
            if (!_streams.TryGetValue(liveEvent.UserId, out var streams)) return;

            // A stream that does not keep up is closed. Its client reconnects and syncs.
            foreach (var slow in streams.FindAll(s => !s.Channel.Writer.TryWrite(liveEvent)))
                Close(liveEvent.UserId, streams, slow);
        }
    }

    public IMessageSubscription Subscribe(long userId)
    {
        var settings = options.CurrentValue;
        var subscription = new Subscription(this, userId, settings.StreamBufferSize);
        lock (_lock)
        {
            if (!_streams.TryGetValue(userId, out var streams)) _streams[userId] = streams = [];
            streams.Add(subscription);

            // LIVE-06: the newest stream wins; the oldest ends.
            while (streams.Count > settings.MaxStreamsPerUser) Close(userId, streams, streams[0]);
        }

        return subscription;
    }

    public void EndStreams(long userId)
    {
        lock (_lock)
        {
            if (!_streams.TryGetValue(userId, out var streams)) return;
            foreach (var stream in streams.ToList()) Close(userId, streams, stream);
        }
    }

    private void Remove(long userId, Subscription subscription)
    {
        lock (_lock)
        {
            if (_streams.TryGetValue(userId, out var streams)) Close(userId, streams, subscription);
        }
    }

    private void Close(long userId, List<Subscription> streams, Subscription subscription)
    {
        subscription.Channel.Writer.TryComplete();
        streams.Remove(subscription);
        if (streams.Count == 0) _streams.Remove(userId);
    }
}
