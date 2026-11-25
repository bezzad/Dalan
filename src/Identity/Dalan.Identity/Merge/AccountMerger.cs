using Dalan.Admin.Contracts;
using Dalan.Identity.Contracts;
using Dalan.Identity.Data;
using Dalan.Identity.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dalan.Identity.Merge;

/// <summary>One side of a merge as the person sees it (AUTH-31).</summary>
internal sealed record MergeSideJson(long Id, string Name, int Bots, int Sites, int Keys, int Chats)
{
    /// <summary>AUTH-32: bots, sites (and demos) and access keys are products; chats are not.</summary>
    public bool HasProducts => Bots + Sites + Keys > 0;
}

/// <summary>
/// AUTH-31: the merge question. <see cref="Keep"/> is "this" or "other" (AUTH-32). <see cref="Refusal"/> is null,
/// "both_have_products", "merge_conflict" (with <see cref="Field"/>) or "suspended".
/// </summary>
internal sealed record MergePlanJson(string Way, string Keep, MergeSideJson This, MergeSideJson Other, string? Refusal, string? Field)
{
    public long KeptId => Keep == "this" ? This.Id : Other.Id;

    public long GoneId => Keep == "this" ? Other.Id : This.Id;
}

/// <summary>AUTH-31 to AUTH-34: the pending merge of a session, the merge itself, and its follow-up in the other services.</summary>
internal sealed class AccountMerger(
    IDbContextFactory<IdentityDb> dbFactory,
    IEnumerable<IUserHoldings> holdings,
    IEnumerable<IUserMergeListener> listeners,
    IAuditLog audit,
    SecurityLog log,
    IOptionsMonitor<IdentityOptions> options,
    TimeProvider time)
{
    public const string ByPhone = "phone";
    public const string ByEmail = "email";
    public const string ByGitHub = "github";

    /// <summary>AUTH-31: after the proof only. A new proof replaces the session's pending merge.</summary>
    public async Task OfferAsync(long sessionId, long userId, long otherUserId, string way, CancellationToken cancellationToken)
    {
        var expires = time.GetUtcNow().AddSeconds(options.CurrentValue.MergeSeconds);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO identity.pending_merges (session_id, user_id, other_user_id, way, expires_at)
            VALUES ({sessionId}, {userId}, {otherUserId}, {way}, {expires})
            ON CONFLICT (session_id) DO UPDATE
            SET user_id = excluded.user_id, other_user_id = excluded.other_user_id, way = excluded.way, expires_at = excluded.expires_at
            """, cancellationToken);
        log.MergeOffered(userId, otherUserId, way);
    }

    public async Task DropAsync(long sessionId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.ExecuteSqlAsync($"DELETE FROM identity.pending_merges WHERE session_id = {sessionId}", cancellationToken);
    }

    /// <summary>AUTH-31: the question for the session's pending merge, or null when there is none (or it ended).</summary>
    public async Task<MergePlanJson?> PlanAsync(long sessionId, long userId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await PendingAsync(db, sessionId, userId, cancellationToken) is not { } pending) return null;
        var (otherId, way) = pending;

        var users = await db.Users.AsNoTracking().Where(u => u.Id == userId || u.Id == otherId).ToListAsync(cancellationToken);
        if (users.Find(u => u.Id == userId) is not { } me || users.Find(u => u.Id == otherId) is not { } other) return null;
        return await PlanAsync(db, me, other, way, cancellationToken);
    }

    /// <summary>
    /// AUTH-32 to AUTH-34: does the pending merge. Null: no pending merge. A plan with a refusal: nothing changed.
    /// Else the kept account has the other's empty fields, the other account is gone, and the other services follow.
    /// </summary>
    public async Task<MergePlanJson?> MergeAsync(long sessionId, long userId, CancellationToken cancellationToken)
    {
        MergePlanJson? plan;
        long mergeId;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            if (await PendingAsync(db, sessionId, userId, cancellationToken) is not { } pending) return null;
            var (otherId, way) = pending;

            // Both rows locked: two merges of one account cannot run side by side.
            var users = await db.Users.FromSql($"SELECT * FROM identity.users WHERE id IN ({userId}, {otherId}) ORDER BY id FOR UPDATE")
                .ToListAsync(cancellationToken);
            if (users.Find(u => u.Id == userId) is not { } me || users.Find(u => u.Id == otherId) is not { } other) return null;

            plan = await PlanAsync(db, me, other, way, cancellationToken);
            if (plan.Refusal is not null) return plan;

            var (kept, gone) = plan.Keep == "this" ? (me, other) : (other, me);
            // The other account goes first: its phone, email, GitHub id and username are unique.
            db.Users.Remove(gone);
            await db.SaveChangesAsync(cancellationToken);
            TakeEmptyFields(kept, gone);
            await db.SaveChangesAsync(cancellationToken);

            var now = time.GetUtcNow();
            mergeId = (await db.Database.SqlQuery<long>($"""
                INSERT INTO identity.merges (from_user_id, into_user_id, way, merged_at)
                VALUES ({gone.Id}, {kept.Id}, {way}, {now})
                RETURNING id AS "Value"
                """).ToListAsync(cancellationToken))[0];
            await transaction.CommitAsync(cancellationToken);
        }

        log.Merged(plan.GoneId, plan.KeptId, plan.Way);
        await audit.RecordAsync(plan.KeptId, "account.merge", $"user {plan.KeptId}",
            $"user {plan.GoneId} merged into user {plan.KeptId} ({plan.Way})", cancellationToken);
        await FollowUpAsync(mergeId, plan.GoneId, plan.KeptId, cancellationToken);
        return plan;
    }

    /// <summary>
    /// AUTH-34: the other services move what they hold (REL-55). A failure is tried again by <see cref="MergeFollowUp"/>;
    /// the merge row is marked done only when every service is done.
    /// </summary>
    public async Task<bool> FollowUpAsync(long mergeId, long fromUserId, long intoUserId, CancellationToken cancellationToken)
    {
        foreach (var listener in listeners)
        {
            try
            {
                await listener.UserMergedAsync(fromUserId, intoUserId, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                log.MergeFollowUpFailed(mergeId, exception);
                return false;
            }
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var now = time.GetUtcNow();
        await db.Database.ExecuteSqlAsync($"UPDATE identity.merges SET done_at = {now} WHERE id = {mergeId}", cancellationToken);
        return true;
    }

    /// <summary>AUTH-33: the kept account takes a field only where its own is empty, and the higher builder state.</summary>
    internal static void TakeEmptyFields(User kept, User gone)
    {
        kept.Phone ??= gone.Phone;
        kept.Email ??= gone.Email;
        kept.GithubId ??= gone.GithubId;
        kept.PasswordHash ??= gone.PasswordHash;
        kept.Username ??= gone.Username;
        if (gone.Builder > kept.Builder)
        {
            kept.Builder = gone.Builder;
            kept.BuilderRequestedAt = gone.BuilderRequestedAt;
        }
    }

    /// <summary>AUTH-33: the first field both accounts have with a different value, or null.</summary>
    internal static string? Conflict(User one, User two) =>
        one.Phone is not null && two.Phone is not null && one.Phone != two.Phone ? "phone"
        : one.Email is not null && two.Email is not null && one.Email != two.Email ? "email"
        : one.GithubId is not null && two.GithubId is not null && one.GithubId != two.GithubId ? "github"
        : null;

    private async Task<MergePlanJson> PlanAsync(IdentityDb db, User me, User other, string way, CancellationToken cancellationToken)
    {
        var mine = await SideAsync(db, me, cancellationToken);
        var theirs = await SideAsync(db, other, cancellationToken);
        var field = Conflict(me, other);
        var refusal = me.SuspendedAt is not null || other.SuspendedAt is not null ? "suspended"
            : mine.HasProducts && theirs.HasProducts ? "both_have_products"
            : field is not null ? "merge_conflict"
            : null;
        // AUTH-32: the account with products stays; with none on either side, the account of this session stays.
        var keep = mine.HasProducts || !theirs.HasProducts ? "this" : "other";
        return new MergePlanJson(way, keep, mine, theirs, refusal, refusal == "merge_conflict" ? field : null);
    }

    private async Task<MergeSideJson> SideAsync(IdentityDb db, User user, CancellationToken cancellationToken)
    {
        var held = new UserHoldings();
        foreach (var service in holdings) held += await service.CountAsync(user.Id, cancellationToken);
        var keys = await db.AccessKeys.CountAsync(k => k.UserId == user.Id, cancellationToken);
        return new MergeSideJson(user.Id, user.DisplayName, held.Bots, held.Sites, keys, held.Chats);
    }

    private async Task<(long OtherUserId, string Way)?> PendingAsync(
        IdentityDb db, long sessionId, long userId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        // One text column ("<id> <way>"), so the query needs no mapped type.
        var rows = await db.Database.SqlQuery<string>($"""
            SELECT other_user_id::text || ' ' || way AS "Value" FROM identity.pending_merges
            WHERE session_id = {sessionId} AND user_id = {userId} AND expires_at > {now}
            """).ToListAsync(cancellationToken);
        if (rows.Count == 0) return null;
        var parts = rows[0].Split(' ', 2);
        return (long.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), parts[1]);
    }
}
