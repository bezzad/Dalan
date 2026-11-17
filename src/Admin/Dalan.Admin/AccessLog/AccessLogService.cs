using System.Collections.Concurrent;
using System.Net;
using Dalan.Admin.Contracts;
using Dalan.Admin.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dalan.Admin.AccessLog;

/// <summary>
/// ADM-01, ADM-02: writes a row when a caller shows up at a new address, or at a known one after the interval.
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
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();

        var now = time.GetUtcNow();
        var key = (actor, actorId, address);
        if (!Claim(key, now)) return;

        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            db.AccessLog.Add(new AccessLogEntry
            {
                Actor = actor,
                ActorId = actorId,
                Address = address,
                Port = port > 0 ? port : null,
                At = now,
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Not written: the next call must try again.
            _lastWritten.TryRemove(key, out _);
            throw;
        }
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

    /// <summary>True for exactly one of the calls that arrive together.</summary>
    private bool Claim((AccessActor, long, IPAddress) key, DateTimeOffset now)
    {
        if (!_lastWritten.TryGetValue(key, out var written)) return _lastWritten.TryAdd(key, now);
        return now - written >= Interval && _lastWritten.TryUpdate(key, now, written);
    }
}
