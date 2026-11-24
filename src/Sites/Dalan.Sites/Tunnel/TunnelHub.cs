using System.Net;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Dalan.Sites.Tunnel;

/// <summary>
/// In memory: the session of each site name, and the requests that wait for an answer.
/// With more than one server this becomes shared, like the bot queue.
/// </summary>
public sealed class TunnelHub(TimeProvider time, IOptionsMonitor<SitesOptions> options)
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, TunnelSession> _bySite = [];
    private readonly Dictionary<string, TunnelSession> _byId = [];
    private readonly Lock _gateLock = new();
    private readonly Dictionary<IPAddress, Gate> _gates = [];
    private long _nextRequestId;

    private TimeSpan Idle => TimeSpan.FromSeconds(options.CurrentValue.SessionIdleSeconds);

    /// <summary>SITE-04: a new session for these sites. It takes each site from its older session.</summary>
    public TunnelSession Open(long userId, IEnumerable<string> sites)
    {
        var session = new TunnelSession(Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)), userId, time.GetUtcNow());
        lock (_lock)
        {
            _byId[session.Id] = session;
            foreach (var site in sites)
            {
                if (_bySite.TryGetValue(site, out var older)) TakeSite(older, site);
                _bySite[site] = session;
                session.AddSite(site);
            }
        }

        return session;
    }

    /// <summary>The caller's session, or null when it is unknown or ended (SITE-04: session_ended).</summary>
    public TunnelSession? Get(string sessionId, long userId)
    {
        lock (_lock)
            return _byId.TryGetValue(sessionId, out var session) && session.UserId == userId && !session.IsEnded ? session : null;
    }

    /// <summary>The alive session of a site, or null (SITE-07). A dead session is ended here.</summary>
    public TunnelSession? Find(string site)
    {
        lock (_lock)
        {
            if (!_bySite.TryGetValue(site, out var session)) return null;
            if (session.IsAlive(time.GetUtcNow(), Idle)) return session;
            EndLocked(session);
            return null;
        }
    }

    public bool IsOnline(string site) => Find(site) is not null;

    /// <summary>SITE-16: the site is gone; its session loses it.</summary>
    public void Drop(string site)
    {
        lock (_lock)
            if (_bySite.TryGetValue(site, out var session)) TakeSite(session, site);
    }

    /// <summary>Ends a session and frees its sites (SITE-15).</summary>
    public void End(TunnelSession session)
    {
        lock (_lock) EndLocked(session);
    }

    /// <summary>Puts a visitor request into the session. The task gives the answer, or null when the session ended.</summary>
    public (long Id, Task<TunnelResponse?> Answer) Send(TunnelSession session, TunnelRequestDraft draft)
    {
        var id = Interlocked.Increment(ref _nextRequestId);
        var answer = new TaskCompletionSource<TunnelResponse?>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Pending[id] = answer;
        if (session.IsEnded || !session.Waiting.Writer.TryWrite(draft.With(id))) answer.TrySetResult(null);
        return (id, answer.Task);
    }

    /// <summary>The request has its answer or was given up.</summary>
    public static void Forget(TunnelSession session, long id) => session.Pending.TryRemove(id, out _);

    /// <summary>
    /// SITE-06 and SITE-22: an answer, or a part of a streamed answer, from the agent. True when it was taken.
    /// A part posted by long polling (<paramref name="viaWebSocket"/> false) completes only after it was written to the visitor.
    /// An answer to a request that already ended is dropped; for a stream over WebSocket the agent is told to stop.
    /// </summary>
    public Task<bool> AnswerAsync(TunnelSession session, TunnelResponse response, bool viaWebSocket, CancellationToken cancellationToken)
    {
        if (session.Pending.TryRemove(response.Id, out var pending))
        {
            if (response.More) session.Streams[response.Id] = new TunnelStream(options.CurrentValue.StreamWindow);
            pending.TrySetResult(response);
            return Task.FromResult(true);
        }

        if (!session.Streams.TryGetValue(response.Id, out var stream) || Decode(response.Body) is not { } bytes)
        {
            if (response.More) EndStream(session, response.Id, cancel: viaWebSocket);
            return Task.FromResult(false);
        }

        var written = viaWebSocket ? null : new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!stream.TryAdd(new StreamPart(bytes, !response.More, written)))
        {
            EndStream(session, response.Id, cancel: true); // More parts than the window: a bad agent.
            return Task.FromResult(false);
        }

        return written is null ? Task.FromResult(true) : written.Task.WaitAsync(cancellationToken);
    }

    /// <summary>SITE-22: the running stream of a request, or null.</summary>
    internal static TunnelStream? StreamOf(TunnelSession session, long id) => session.Streams.GetValueOrDefault(id);

    /// <summary>SITE-22: one part was written to the visitor; over WebSocket the agent may send one more.</summary>
    internal static void Written(TunnelSession session, long id, StreamPart part)
    {
        if (part.Written is { } written) written.TrySetResult(true);
        else session.Control.Writer.TryWrite(new TunnelControl(id, Ack: true));
    }

    /// <summary>SITE-22: the stream is over. With <paramref name="cancel"/> the agent is told to stop reading the app.</summary>
    internal static void EndStream(TunnelSession session, long id, bool cancel)
    {
        if (session.Streams.TryRemove(id, out var stream)) stream.End();
        if (cancel) session.Control.Writer.TryWrite(new TunnelControl(id, Cancel: true));
    }

    /// <summary>A request that is still waiting for its answer (the agent may skip the others).</summary>
    public static bool IsPending(TunnelSession session, long id) => session.Pending.ContainsKey(id);

    /// <summary>
    /// SITE-12: a place for one more request of this address at the agent. When all places are taken it waits in line,
    /// in order, for at most the request timeout. False when the line is full or the wait was too long.
    /// </summary>
    public async Task<bool> EnterAsync(IPAddress? address, CancellationToken cancellationToken)
    {
        if (address is null) return true;
        var settings = options.CurrentValue;
        Gate? gate;
        TaskCompletionSource<bool> turn;
        LinkedListNode<TaskCompletionSource<bool>> place;
        lock (_gateLock)
        {
            if (!_gates.TryGetValue(address, out gate)) _gates[address] = gate = new Gate();
            if (gate.Running < settings.MaxPendingPerAddress)
            {
                gate.Running++;
                return true;
            }

            if (gate.Line.Count >= settings.MaxQueuedPerAddress) return false;
            turn = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            place = gate.Line.AddLast(turn);
        }

        try
        {
            return await turn.Task.WaitAsync(TimeSpan.FromSeconds(settings.RequestTimeoutSeconds), time, cancellationToken);
        }
        catch (Exception exception) when (exception is TimeoutException or OperationCanceledException)
        {
            lock (_gateLock)
            {
                // The place may have come at the same moment: give it on.
                if (turn.Task.IsCompleted) LeaveLocked(address);
                else gate.Line.Remove(place);
            }

            if (exception is OperationCanceledException) throw;
            return false;
        }
    }

    /// <summary>SITE-12: a request of this address has ended; its place goes to the first one in line.</summary>
    public void Leave(IPAddress? address)
    {
        if (address is null) return;
        lock (_gateLock) LeaveLocked(address);
    }

    private void LeaveLocked(IPAddress address)
    {
        if (!_gates.TryGetValue(address, out var gate)) return;
        if (gate.Line.First is { } first)
        {
            gate.Line.RemoveFirst();
            first.Value.TrySetResult(true);
            return;
        }

        if (--gate.Running <= 0) _gates.Remove(address);
    }

    private static byte[]? Decode(string? body)
    {
        if (body is null) return [];
        try
        {
            return Convert.FromBase64String(body);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>SITE-12: the requests of one address at the agent, and the ones waiting in line.</summary>
    private sealed class Gate
    {
        public int Running;
        public readonly LinkedList<TaskCompletionSource<bool>> Line = new();
    }

    private void TakeSite(TunnelSession session, string site)
    {
        _bySite.Remove(site);
        if (session.RemoveSite(site)) EndLocked(session);
    }

    private void EndLocked(TunnelSession session)
    {
        session.End();
        _byId.Remove(session.Id);
        foreach (var site in _bySite.Where(pair => pair.Value == session).Select(pair => pair.Key).ToList())
            _bySite.Remove(site);
    }
}

/// <summary>A visitor request before it has its id.</summary>
public sealed record TunnelRequestDraft(string Site, string Method, string Path, Dictionary<string, string[]> Headers, string? Body)
{
    public TunnelRequest With(long id) => new(id, Site, Method, Path, Headers, Body);
}
