using Dalan.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dalan.Identity.Merge;

/// <summary>
/// AUTH-34: the outbox of merges. A merge whose follow-up failed (or stopped with the server) is tried again every
/// Identity:MergeRetrySeconds, until every service is done. Only merges older than that time, so the call that
/// made the merge has finished its own try.
/// </summary>
internal sealed class MergeFollowUp(
    IDbContextFactory<IdentityDb> dbFactory,
    AccountMerger merger,
    IOptionsMonitor<IdentityOptions> options,
    TimeProvider time) : BackgroundService
{
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var before = time.GetUtcNow().AddSeconds(-options.CurrentValue.MergeRetrySeconds);
        List<string> waiting;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            // One text column ("<id> <from> <into>"), so the query needs no mapped type.
            waiting = await db.Database.SqlQuery<string>($"""
                SELECT id::text || ' ' || from_user_id::text || ' ' || into_user_id::text AS "Value"
                FROM identity.merges WHERE done_at IS NULL AND merged_at <= {before} ORDER BY id
                """).ToListAsync(cancellationToken);
        }

        foreach (var row in waiting)
        {
            var ids = Array.ConvertAll(row.Split(' '), part => long.Parse(part, System.Globalization.CultureInfo.InvariantCulture));
            await merger.FollowUpAsync(ids[0], ids[1], ids[2], cancellationToken);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, options.CurrentValue.MergeRetrySeconds)), time, stoppingToken);
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // A failed round (database away) is tried again in the next one.
            catch (Exception)
#pragma warning restore CA1031
            {
            }
        }
    }
}
