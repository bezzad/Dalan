using System.Collections.Concurrent;
using System.Net;
using Dalan.Admin.Contracts;
using Dalan.Admin.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dalan.Admin.AccessLog;

/// <summary>
/// ADM-01, ADM-02, ADM-04, ADM-16: writes a row when a caller shows up at a new address, or at a known one after the interval.
/// "Last written" is kept in memory, so almost every call costs one dictionary read.
/// With several instances each has its own memory: there can be more rows, never fewer.
/// </summary>
public sealed class AccessLogService(
    IDbContextFactory<AdminDb> dbFactory, IOptionsMonitor<AdminOptions> options, TimeProvider time) : IAccessLog
{
    private readonly ConcurrentDictionary<(AccessActor Actor, long ActorId, IPAddress Address), DateTimeOffset> _lastWritten = new();

    private TimeSpan Interval => TimeSpan.FromMinutes(options.CurrentValue.AccessLogIntervalMinutes);

    public async ValueTask RecordAsync(
        AccessActor actor, long actorId, IPAddress? address, int port, CancellationToken cancellationToken)
    {
        // Only the in-process test server has no address.
        if (address is null) return;
        address = Normalize(address);

        var now = time.GetUtcNow();
        var key = (actor, actorId, address);
        if (!Claim(key, now)) return;

        try
        {
            await InsertAsync(new AccessLogEntry { Actor = actor, ActorId = actorId, Address = address, Port = Port(port), At = now }, cancellationToken);
        }
        catch
        {
            // Not written: the next call must try again.
            _lastWritten.TryRemove(key, out _);
            throw;
        }
    }

    public async ValueTask RecordSmsAsync(string phone, IPAddress? address, int port, CancellationToken cancellationToken)
    {
        if (address is null) return;

        await InsertAsync(
            new AccessLogEntry { Actor = AccessActor.Phone, Phone = phone, Address = Normalize(address), Port = Port(port), At = time.GetUtcNow() },
            cancellationToken);
    }

    public async ValueTask RecordEmailAsync(string email, IPAddress? address, int port, CancellationToken cancellationToken)
    {
        if (address is null) return;

        await InsertAsync(
            new AccessLogEntry { Actor = AccessActor.Email, Email = email, Address = Normalize(address), Port = Port(port), At = time.GetUtcNow() },
            cancellationToken);
    }

    /// <summary>ADM-03: deletes rows past the retention time, and forgets memory entries that no longer matter.</summary>
    public async Task SweepOnceAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        foreach (var (key, written) in _lastWritten)
        {
            if (now - written >= Interval) _lastWritten.TryRemove(new(key, written));
        }

        var oldest = now.AddDays(-options.CurrentValue.AccessLogRetentionDays);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.AccessLog.Where(e => e.At < oldest).ExecuteDeleteAsync(cancellationToken);
    }

    private async Task InsertAsync(AccessLogEntry entry, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.AccessLog.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>An IPv4 address that arrived as IPv6 (::ffff:1.2.3.4) is stored as IPv4.</summary>
    private static IPAddress Normalize(IPAddress address) => address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    /// <summary>Null when the proxy did not report the port.</summary>
    private static int? Port(int port) => port > 0 ? port : null;

    /// <summary>True for exactly one of the calls that arrive together.</summary>
    private bool Claim((AccessActor, long, IPAddress) key, DateTimeOffset now)
    {
        if (!_lastWritten.TryGetValue(key, out var written)) return _lastWritten.TryAdd(key, now);
        return now - written >= Interval && _lastWritten.TryUpdate(key, now, written);
    }
}
