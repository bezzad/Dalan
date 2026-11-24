using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Dalan.Sites.Tunnel;

/// <summary>
/// One open agent connection (SITE-04): the sites it serves, the requests waiting to be taken,
/// and the requests the agent took but did not answer yet.
/// </summary>
#pragma warning disable CA1001 // The token source lives as long as the session; readers hold its token after the end, so it is never disposed.
public sealed class TunnelSession(string id, long userId, DateTimeOffset now)
{
    private readonly Lock _lock = new();
    private readonly HashSet<string> _sites = [];
    private readonly CancellationTokenSource _ended = new();
    private int _readers;
    private DateTimeOffset _lastSeen = now;

    public string Id { get; } = id;
    public long UserId { get; } = userId;

    /// <summary>Requests the agent has not taken yet.</summary>
    internal Channel<TunnelRequest> Waiting { get; } = Channel.CreateUnbounded<TunnelRequest>();

    /// <summary>Requests sent into this session that have no answer yet, by request id.</summary>
    internal ConcurrentDictionary<long, TaskCompletionSource<TunnelResponse?>> Pending { get; } = new();

    /// <summary>SITE-22: answers that came in parts and are still running, by request id.</summary>
    internal ConcurrentDictionary<long, TunnelStream> Streams { get; } = new();

    /// <summary>
    /// SITE-22: "ack" and "cancel" for the WebSocket. Only a WebSocket reads it; an agent on long polling never makes an ack,
    /// and the oldest message goes when it is full, so it cannot grow without end.
    /// </summary>
    internal Channel<TunnelControl> Control { get; } = Channel.CreateBounded<TunnelControl>(
        new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.DropOldest });

    /// <summary>Cancelled when the session ends: replaced, deleted, or its user lost access.</summary>
    public CancellationToken Ended => _ended.Token;

    public bool IsEnded => _ended.IsCancellationRequested;

    internal void AddSite(string name)
    {
        lock (_lock) _sites.Add(name);
    }

    /// <summary>Takes a site away. True when the session has no site left.</summary>
    internal bool RemoveSite(string name)
    {
        lock (_lock)
        {
            _sites.Remove(name);
            return _sites.Count == 0;
        }
    }

    /// <summary>SITE-07: a WebSocket or a poll is reading now (every reader calls <see cref="StopReading"/>).</summary>
    internal void StartReading(DateTimeOffset now)
    {
        lock (_lock)
        {
            _readers++;
            _lastSeen = now;
        }
    }

    internal void StopReading(DateTimeOffset now)
    {
        lock (_lock)
        {
            _readers--;
            _lastSeen = now;
        }
    }

    /// <summary>SITE-07: alive while someone reads, and for the idle time after the last read.</summary>
    internal bool IsAlive(DateTimeOffset now, TimeSpan idle)
    {
        if (IsEnded) return false;
        lock (_lock) return _readers > 0 || now - _lastSeen < idle;
    }

    /// <summary>Ends the session. Every waiting visitor gets the offline answer at once; running streams stop.</summary>
    internal void End()
    {
        if (IsEnded) return;
        _ended.Cancel();
        Waiting.Writer.TryComplete();
        foreach (var pending in Pending.Values) pending.TrySetResult(null);
        foreach (var stream in Streams.Values) stream.End();
    }
}
#pragma warning restore CA1001
