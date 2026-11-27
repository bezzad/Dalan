using System.Security.Claims;
using Dalan.Admin.Contracts;
using Dalan.Identity.Codes;
using Dalan.Identity.Data;
using Dalan.Identity.EmailLogin;
using Dalan.Identity.GitHubLogin;
using Dalan.Identity.Sessions;
using Dalan.Identity.SmsLogin;
using Dalan.Identity.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Dalan.Identity.Merge;

/// <summary>Link a login way (AUTH-30) and merge two accounts of one person (AUTH-31 to AUTH-34). Sessions only, never an access key.</summary>
internal static class MergeEndpoints
{
    private sealed record EmailRequest(string? Email, string? Code);

    public static void MapMerge(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/account/merge", GetAsync).RequireAuthorization();
        endpoints.MapPost("/api/account/merge", MergeAsync).RequireAuthorization();
        endpoints.MapDelete("/api/account/merge", DropAsync).RequireAuthorization();
        endpoints.MapPost("/api/account/email", LinkEmailAsync).RequireAuthorization();
        endpoints.MapGet("/api/account/github", LinkGitHubAsync).RequireAuthorization();
    }

    /// <summary>AUTH-26, AUTH-30, AUTH-31: the proof is done and the way belongs to another account: a pending merge.</summary>
    public static async Task<IResult> OfferAsync(
        HttpContext http, SessionTokens sessions, AccountMerger merger, long userId, long otherUserId, string way)
    {
        if (await sessions.SessionIdAsync(http, userId) is not { } sessionId) return AuthJson.Unauthorized();
        await merger.OfferAsync(sessionId, userId, otherUserId, way, http.RequestAborted);
        return AuthJson.Error(StatusCodes.Status409Conflict, "merge_needed",
            $"This {WayName(way)} belongs to another Dalan account, and you proved it is yours. " +
            "GET /api/account/merge shows the merge; POST it to merge the two accounts, or DELETE it to cancel.");
    }

    private static async Task<IResult> GetAsync(HttpContext http, SessionTokens sessions, AccountMerger merger)
    {
        var userId = SessionTokens.UserId(http.User);
        if (await sessions.SessionIdAsync(http, userId) is not { } sessionId) return AuthJson.Unauthorized();
        return await merger.PlanAsync(sessionId, userId, http.RequestAborted) is { } plan ? AuthJson.Ok(plan) : NoMerge();
    }

    /// <summary>AUTH-34: the person is then logged in as the kept account.</summary>
    private static async Task<IResult> MergeAsync(
        HttpContext http, SessionTokens sessions, AccountMerger merger, UserAdmin users, IAccessLog accessLog)
    {
        var userId = SessionTokens.UserId(http.User);
        // ADM-01: this door knows its caller.
        await accessLog.RecordAsync(AccessActor.User, userId, http.Connection.RemoteIpAddress, http.Connection.RemotePort, http.RequestAborted);
        if (await sessions.SessionIdAsync(http, userId) is not { } sessionId) return AuthJson.Unauthorized();
        if (await merger.MergeAsync(sessionId, userId, http.RequestAborted) is not { } plan) return NoMerge();

        if (plan.Refusal is { } refusal) return Refused(refusal, plan.Field);
        await users.ApproveByPhoneAsync(plan.KeptId, http.RequestAborted);
        if (plan.KeptId != userId)
            await sessions.SignInAsync(http, plan.KeptId, http.User.FindFirstValue(SessionTokens.MethodClaim) ?? SessionTokens.ByDevLogin);
        return AuthJson.Ok(await users.ProfileAsync(plan.KeptId, http.RequestAborted));
    }

    private static async Task<IResult> DropAsync(HttpContext http, SessionTokens sessions, AccountMerger merger)
    {
        var userId = SessionTokens.UserId(http.User);
        if (await sessions.SessionIdAsync(http, userId) is not { } sessionId) return AuthJson.Unauthorized();
        await merger.DropAsync(sessionId, http.RequestAborted);
        return Results.NoContent();
    }

    /// <summary>AUTH-30: an email code proves the address; a free address is added, one of another account makes a pending merge.</summary>
    private static async Task<IResult> LinkEmailAsync(
        HttpContext http, IDbContextFactory<IdentityDb> dbFactory, EmailCodes codes, SessionTokens sessions,
        AccountMerger merger, UserAdmin users, SecurityLog log, IAccessLog accessLog)
    {
        var request = await AuthJson.ReadAsync<EmailRequest>(http);
        if (EmailAddress.Normalize(request?.Email) is not { } email) return EmailLoginEndpoints.BadEmail();

        var userId = SessionTokens.UserId(http.User);
        // ADM-01: this door knows its caller.
        await accessLog.RecordAsync(AccessActor.User, userId, http.Connection.RemoteIpAddress, http.Connection.RemotePort, http.RequestAborted);
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        if (await db.Users.AnyAsync(u => u.Id == userId && u.Email != null, http.RequestAborted))
            return AuthJson.Error(StatusCodes.Status409Conflict, "has_email", "This account has an email address already.");

        var (check, codeId) = await codes.CheckAsync(email, request!.Code?.Trim() ?? "", http.RequestAborted);
        if (check == CodeCheck.Wrong || !await codes.UseAsync(codeId, http.RequestAborted)) return SmsLoginEndpoints.WrongCode();

        if (await OwnerAsync(db, u => u.Email == email, http.RequestAborted) is { } other && other != userId)
            return await OfferAsync(http, sessions, merger, userId, other, AccountMerger.ByEmail);
        try
        {
            await db.Users.Where(u => u.Id == userId && u.Email == null)
                .ExecuteUpdateAsync(set => set.SetProperty(u => u.Email, email), http.RequestAborted);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // Another account took the address in the same moment.
            if (await OwnerAsync(db, u => u.Email == email, http.RequestAborted) is { } owner)
                return await OfferAsync(http, sessions, merger, userId, owner, AccountMerger.ByEmail);
            throw;
        }

        log.WayLinked(userId, AccountMerger.ByEmail);
        return AuthJson.Ok(await users.ProfileAsync(userId, http.RequestAborted));
    }

    /// <summary>AUTH-30: starts the GitHub login; its way back links instead of logging in (see the GitHub callback).</summary>
    private static async Task<IResult> LinkGitHubAsync(
        HttpContext http, IDbContextFactory<IdentityDb> dbFactory, SessionTokens sessions,
        IOptionsMonitor<IdentityOptions> options, TimeProvider time)
    {
        var github = options.CurrentValue.GitHub;
        if (!github.IsOn) return Results.NotFound();

        var userId = SessionTokens.UserId(http.User);
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        if (await db.Users.AnyAsync(u => u.Id == userId && u.GithubId != null, http.RequestAborted))
            return Results.Redirect("/account?link_error=has_github");
        if (await sessions.SessionIdAsync(http, userId) is not { } sessionId) return AuthJson.Unauthorized();

        var state = GitHubLoginEndpoints.NewState();
        var expires = time.GetUtcNow().AddSeconds(github.StateSeconds);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO identity.github_links (state_hash, session_id, expires_at)
            VALUES ({GitHubLoginEndpoints.StateHash(state)}, {sessionId}, {expires})
            """, http.RequestAborted);
        return GitHubLoginEndpoints.Authorize(http, github, state);
    }

    public static async Task<long?> OwnerAsync(
        IdentityDb db, System.Linq.Expressions.Expression<Func<User, bool>> match, CancellationToken cancellationToken) =>
        await db.Users.Where(match).Select(u => (long?)u.Id).FirstOrDefaultAsync(cancellationToken);

    private static string WayName(string way) => way switch
    {
        AccountMerger.ByPhone => "mobile number",
        AccountMerger.ByEmail => "email address",
        _ => "GitHub account",
    };

    private static IResult NoMerge() => AuthJson.Error(StatusCodes.Status404NotFound, "no_merge",
        "There is no merge waiting, or it ended. Prove the mobile number, email or GitHub account again.");

    private static IResult Refused(string refusal, string? field) => refusal switch
    {
        "both_have_products" => AuthJson.Error(StatusCodes.Status409Conflict, "both_have_products",
            "Both accounts have bots, sites or access keys. Dalan does not move them, so these accounts cannot be merged."),
        "merge_conflict" => AuthJson.Error(StatusCodes.Status409Conflict, "merge_conflict",
            $"Both accounts have a different {field}. An account has only one, so these accounts cannot be merged."),
        "admin" => AuthJson.Error(StatusCodes.Status403Forbidden, "admin_merge",
            "One of these accounts is an admin of Dalan. An admin account cannot be merged."),
        _ => AuthJson.Suspended(),
    };
}
