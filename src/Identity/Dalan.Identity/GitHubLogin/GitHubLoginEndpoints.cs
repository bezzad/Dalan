using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dalan.Admin.Contracts;
using Dalan.Identity.Data;
using Dalan.Identity.Sessions;
using Dalan.Identity.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Dalan.Identity.Merge;

namespace Dalan.Identity.GitHubLogin;

/// <summary>Level 1 login with GitHub: start (AUTH-27), come back (AUTH-28), failures (AUTH-29).</summary>
internal static class GitHubLoginEndpoints
{
    /// <summary>Holds the state between the start and the way back. Lax: the way back is a navigation from github.com.</summary>
    public const string StateCookie = "__Host-dalan_github";

    public static void MapGitHubLogin(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/auth/github", Start);
        endpoints.MapGet("/api/auth/github/callback", CallbackAsync);
    }

    private static IResult Start(HttpContext http, IOptionsMonitor<IdentityOptions> options)
    {
        var github = options.CurrentValue.GitHub;
        return github.IsOn ? Authorize(http, github, NewState()) : Results.NotFound();
    }

    /// <summary>A new random state of 256 bits (AUTH-27).</summary>
    public static string NewState() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>AUTH-30: a GitHub link is found by the hash of its state, never by the state itself.</summary>
    public static byte[] StateHash(string state) => SHA256.HashData(Encoding.UTF8.GetBytes(state));

    /// <summary>AUTH-27: keeps the state in the cookie and sends the browser to GitHub.</summary>
    public static IResult Authorize(HttpContext http, GitHubOptions github, string state)
    {
        http.Response.Cookies.Append(StateCookie, state, Cookie(TimeSpan.FromSeconds(github.StateSeconds)));
        return Results.Redirect(QueryHelpers.AddQueryString($"{github.WebUrl}/login/oauth/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = github.ClientId,
            ["redirect_uri"] = github.CallbackUrl,
            ["scope"] = "user:email",
            ["state"] = state,
        }));
    }

    private static async Task<IResult> CallbackAsync(
        HttpContext http,
        GitHubClient github,
        SessionTokens sessions,
        SecurityLog log,
        IAccessLog accessLog,
        IDbContextFactory<IdentityDb> dbFactory,
        IOptionsMonitor<IdentityOptions> options,
        UserAdmin users,
        AccountMerger merger,
        TimeProvider time)
    {
        if (!options.CurrentValue.GitHub.IsOn) return Results.NotFound();

        var expected = http.Request.Cookies[StateCookie];
        http.Response.Cookies.Delete(StateCookie, Cookie(lifetime: null));
        var query = http.Request.Query;
        if (!SameState(expected, query["state"])) return Fail(log, "github_state");
        if (!string.IsNullOrEmpty(query["error"])) return Fail(log, "github_denied");

        GitHubUser account;
        try
        {
            account = await github.GetUserAsync(query["code"].ToString(), http.RequestAborted);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException
            || (exception is TaskCanceledException && !http.RequestAborted.IsCancellationRequested))
        {
            return Fail(log, "github_unavailable");
        }

        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        // AUTH-30: a link started in the account screen. Found by the state, because the session cookie is not sent from github.com.
        if (await TakeLinkAsync(db, expected!, time.GetUtcNow(), http.RequestAborted) is { } link)
            return await LinkAsync(http, db, link.SessionId, link.UserId, account, merger, log, accessLog);

        if (account.Email is not { } email) return Fail(log, "github_no_email");

        var userId = await db.Users.Where(u => u.GithubId == account.Id).Select(u => (long?)u.Id).FirstOrDefaultAsync(http.RequestAborted);
        if (userId is null)
        {
            // The same verified email is the same person: a known user gets the GitHub id and keeps the name.
            var max = options.CurrentValue.MaxNameLength;
            var name = account.Name.Length == 0 ? "GitHub" : account.Name[..Math.Min(account.Name.Length, max)];
            var now = time.GetUtcNow();
            var ids = await db.Database.SqlQuery<long>($"""
                INSERT INTO identity.users (email, github_id, display_name, created_at)
                VALUES ({email}, {account.Id}, {name}, {now})
                ON CONFLICT (email) DO UPDATE SET github_id = EXCLUDED.github_id
                RETURNING id AS "Value"
                """).ToListAsync(http.RequestAborted);
            userId = ids[0];
        }

        if ((await users.GetAccessAsync(userId.Value, http.RequestAborted)).IsSuspended) return Fail(log, "suspended");

        // ADM-01: this door knows its caller, so it records the real address before the login.
        await accessLog.RecordAsync(
            AccessActor.User, userId.Value, http.Connection.RemoteIpAddress, http.Connection.RemotePort, http.RequestAborted);
        await sessions.SignInAsync(http, userId.Value, SessionTokens.ByGitHub);
        return Results.Redirect("/");
    }

    /// <summary>The link of this state, used once; null when there is none or it ended.</summary>
    private static async Task<(long SessionId, long UserId)?> TakeLinkAsync(
        IdentityDb db, string state, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var rows = await db.Database.SqlQuery<string>($"""
            DELETE FROM identity.github_links AS l USING identity.sessions AS s
            WHERE l.state_hash = {StateHash(state)} AND l.session_id = s.id AND l.expires_at > {now}
            RETURNING s.id::text || ' ' || s.user_id::text AS "Value"
            """).ToListAsync(cancellationToken);
        if (rows.Count == 0) return null;
        var ids = Array.ConvertAll(rows[0].Split(' '), part => long.Parse(part, System.Globalization.CultureInfo.InvariantCulture));
        return (ids[0], ids[1]);
    }

    /// <summary>AUTH-30: a free GitHub account is added; one of another account makes a pending merge (AUTH-31).</summary>
    private static async Task<IResult> LinkAsync(
        HttpContext http, IdentityDb db, long sessionId, long userId, GitHubUser account,
        AccountMerger merger, SecurityLog log, IAccessLog accessLog)
    {
        // ADM-01: this door knows its caller.
        await accessLog.RecordAsync(
            AccessActor.User, userId, http.Connection.RemoteIpAddress, http.Connection.RemotePort, http.RequestAborted);

        var owner = await MergeEndpoints.OwnerAsync(db, u => u.GithubId == account.Id, http.RequestAborted);
        if (owner == userId) return Results.Redirect("/account");
        if (owner is null)
        {
            try
            {
                var linked = await db.Users.Where(u => u.Id == userId && u.GithubId == null)
                    .ExecuteUpdateAsync(set => set.SetProperty(u => u.GithubId, account.Id), http.RequestAborted);
                if (linked == 0) return Results.Redirect("/account?link_error=has_github");
                log.WayLinked(userId, AccountMerger.ByGitHub);
                return Results.Redirect("/account");
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                // Another account took this GitHub account in the same moment.
                owner = await MergeEndpoints.OwnerAsync(db, u => u.GithubId == account.Id, http.RequestAborted);
                if (owner is null) throw;
            }
        }

        await merger.OfferAsync(sessionId, userId, owner.Value, AccountMerger.ByGitHub, http.RequestAborted);
        return Results.Redirect("/account?merge=1");
    }

    /// <summary>The callback is a page load, not a JSON call: the page shows the error (CLI-36).</summary>
    private static IResult Fail(SecurityLog log, string code)
    {
        log.LoginFailed(SessionTokens.ByGitHub, code);
        return Results.Redirect($"/?login_error={code}");
    }

    private static bool SameState(string? expected, string? given) =>
        !string.IsNullOrEmpty(expected) && !string.IsNullOrEmpty(given)
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(given));

    private static CookieOptions Cookie(TimeSpan? lifetime) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        MaxAge = lifetime,
    };
}
