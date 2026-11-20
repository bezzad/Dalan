using System.Collections.Concurrent;
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
    private readonly ConcurrentDictionary<IPAddress, int> _perAddress = new();
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

    /// <summary>SITE-06: an answer from the agent. An answer to a request that already ended is dropped.</summary>
    public static void Answer(TunnelSession session, TunnelResponse response)
    {
        if (session.Pending.TryRemove(response.Id, out var pending)) pending.TrySetResult(response);
    }

    /// <summary>A request that is still waiting for its answer (the agent may skip the others).</summary>
    public static bool IsPending(TunnelSession session, long id) => session.Pending.ContainsKey(id);

    /// <summary>SITE-12: one more waiting request from this address, unless it has too many.</summary>
    public bool TryEnter(IPAddress? address)
    {
        if (address is null) return true;
        var count = _perAddress.AddOrUpdate(address, 1, (_, n) => n + 1);
        if (count <= options.CurrentValue.MaxPendingPerAddress) return true;
        Leave(address);
        return false;
    }

    public void Leave(IPAddress? address)
    {
        if (address is null) return;
        if (_perAddress.AddOrUpdate(address, 0, (_, n) => n - 1) <= 0) _perAddress.TryRemove(new(address, 0));
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
