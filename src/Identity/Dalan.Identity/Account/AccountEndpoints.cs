using Dalan.Admin.Contracts;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Dalan.Identity.Codes;
using Dalan.Identity.Contracts;
using Dalan.Identity.Data;
using Dalan.Identity.Merge;
using Dalan.Identity.Passwords;
using Dalan.Identity.Sessions;
using Dalan.Identity.SmsLogin;
using Dalan.Identity.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Dalan.Identity.Account;

/// <summary>What a logged-in user changes on the account: the password (AUTH-13), the username (AUTH-15) and the phone (AUTH-26).</summary>
internal static partial class AccountEndpoints
{
    private sealed record PasswordRequest(string? Password, string? CurrentPassword);

    private sealed record UsernameRequest(string? Username);

    private sealed record PhoneRequest(string? Phone, string? Code);

    public static void MapAccount(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/account/password", SetPasswordAsync).RequireAuthorization();
        endpoints.MapPost("/api/account/username", SetUsernameAsync).RequireAuthorization();
        endpoints.MapPost("/api/account/builder", AskBuilderAsync).RequireAuthorization();
        endpoints.MapPost("/api/account/phone", AddPhoneAsync).RequireAuthorization();
    }

    private static async Task<IResult> SetPasswordAsync(
        HttpContext http,
        IDbContextFactory<IdentityDb> dbFactory,
        PasswordHasher hasher,
        SessionTokens sessions,
        SecurityLog log,
        IOptionsMonitor<IdentityOptions> options)
    {
        var settings = options.CurrentValue;
        var request = await AuthJson.ReadAsync<PasswordRequest>(http);
        var password = request?.Password ?? "";
        if (password.Length < settings.PasswordMinLength || password.Length > settings.PasswordMaxLength)
        {
            return AuthJson.BadRequest("weak_password",
                $"""Send JSON with "password": {settings.PasswordMinLength} to {settings.PasswordMaxLength} characters.""");
        }

        var userId = SessionTokens.UserId(http.User);
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, http.RequestAborted);
        if (user is null) return AuthJson.Unauthorized();

        // A session that started with an SMS or email code has proved the phone or email: that is the "forgot my password" path.
        var provedByCode = http.User.FindFirstValue(SessionTokens.MethodClaim) is SessionTokens.BySms or SessionTokens.ByEmail;
        if (user.PasswordHash is { } stored && !provedByCode)
        {
            var current = request?.CurrentPassword ?? "";
            if (current.Length == 0 || current.Length > settings.PasswordMaxLength || !await hasher.VerifyAsync(stored, current))
            {
                return AuthJson.Error(StatusCodes.Status403Forbidden, "wrong_password",
                    """Send the right "current_password" too. If you forgot it, log in with an SMS code and set a new one.""");
            }
        }

        user.PasswordHash = await hasher.HashAsync(password);
        await db.SaveChangesAsync(http.RequestAborted);
        await sessions.SignOutOtherDevicesAsync(http, userId);
        log.PasswordSet(userId);
        return Results.NoContent();
    }

    private static async Task<IResult> SetUsernameAsync(HttpContext http, IDbContextFactory<IdentityDb> dbFactory, UserAdmin users)
    {
        var username = (await AuthJson.ReadAsync<UsernameRequest>(http))?.Username?.Trim() ?? "";
        if (!UsernameShape().IsMatch(username))
        {
            return AuthJson.BadRequest("bad_username",
                """Send JSON with "username": 5 to 32 characters of a-z, 0-9 and _, starting with a letter.""");
        }

        var userId = SessionTokens.UserId(http.User);
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
            await db.Users.Where(u => u.Id == userId)
                .ExecuteUpdateAsync(set => set.SetProperty(u => u.Username, username), http.RequestAborted);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return AuthJson.Error(StatusCodes.Status409Conflict, "username_taken", "Another user has this username. Choose another one.");
        }

        var profile = await users.ProfileAsync(userId, http.RequestAborted);
        return profile is null ? AuthJson.Unauthorized() : AuthJson.Ok(profile);
    }

    /// <summary>
    /// AUTH-19: asks for the right to create bots. Needs a phone (level 2). With Identity:AutoBuilder on (AUTH-39) "none"
    /// is approved at once; a refused user's new request waits for the admin. An approved user stays approved.
    /// </summary>
    private static async Task<IResult> AskBuilderAsync(
        HttpContext http, IDbContextFactory<IdentityDb> dbFactory, UserAdmin users, TimeProvider time,
        IOptionsMonitor<IdentityOptions> options)
    {
        var userId = SessionTokens.UserId(http.User);
        await using (var db = await dbFactory.CreateDbContextAsync(http.RequestAborted))
        {
            if (await db.Users.AnyAsync(u => u.Id == userId && u.Phone == null, http.RequestAborted))
            {
                return AuthJson.Error(StatusCodes.Status403Forbidden, "phone_required",
                    "To build bots, first verify your mobile number in the account screen.");
            }

            var auto = options.CurrentValue.AutoBuilder;
            await db.Users.Where(u => u.Id == userId && (u.Builder == BuilderState.None || u.Builder == BuilderState.Refused))
                .ExecuteUpdateAsync(set => set
                    .SetProperty(u => u.Builder, u => auto && u.Builder == BuilderState.None ? BuilderState.Approved : BuilderState.Requested)
                    .SetProperty(u => u.BuilderRequestedAt, time.GetUtcNow()), http.RequestAborted);
        }

        var profile = await users.ProfileAsync(userId, http.RequestAborted);
        return profile is null ? AuthJson.Unauthorized() : AuthJson.Ok(profile);
    }

    /// <summary>
    /// AUTH-26: a level 1 user proves a phone with an SMS code (sent by POST /api/auth/sms) and becomes level 2.
    /// A phone of another account makes a pending merge instead (AUTH-31).
    /// </summary>
    private static async Task<IResult> AddPhoneAsync(
        HttpContext http, IDbContextFactory<IdentityDb> dbFactory, SmsCodes codes, UserAdmin users, SecurityLog log,
        SessionTokens sessions, AccountMerger merger, IAccessLog accessLog)
    {
        var request = await AuthJson.ReadAsync<PhoneRequest>(http);
        if (PhoneNumber.Normalize(request?.Phone) is not { } phone) return SmsLoginEndpoints.BadPhone();

        var userId = SessionTokens.UserId(http.User);
        // ADM-01: this door knows its caller.
        await accessLog.RecordAsync(AccessActor.User, userId, http.Connection.RemoteIpAddress, http.Connection.RemotePort, http.RequestAborted);
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        if (await db.Users.AnyAsync(u => u.Id == userId && u.Phone != null, http.RequestAborted))
            return AuthJson.Error(StatusCodes.Status409Conflict, "has_phone", "This account has a mobile number already.");

        var (check, codeId) = await codes.CheckAsync(phone, request!.Code?.Trim() ?? "", http.RequestAborted);
        if (check == CodeCheck.Wrong || !await codes.UseAsync(codeId, http.RequestAborted)) return SmsLoginEndpoints.WrongCode();

        if (await MergeEndpoints.OwnerAsync(db, u => u.Phone == phone, http.RequestAborted) is { } other)
            return await MergeEndpoints.OfferAsync(http, sessions, merger, userId, other, AccountMerger.ByPhone);
        try
        {
            await db.Users.Where(u => u.Id == userId && u.Phone == null)
                .ExecuteUpdateAsync(set => set.SetProperty(u => u.Phone, phone), http.RequestAborted);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // Another account took the number in the same moment.
            if (await MergeEndpoints.OwnerAsync(db, u => u.Phone == phone, http.RequestAborted) is { } owner)
                return await MergeEndpoints.OfferAsync(http, sessions, merger, userId, owner, AccountMerger.ByPhone);
            throw;
        }

        log.PhoneAdded(userId);
        await users.ApproveByPhoneAsync(userId, http.RequestAborted);
        var profile = await users.ProfileAsync(userId, http.RequestAborted);
        return profile is null ? AuthJson.Unauthorized() : AuthJson.Ok(profile);
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]{4,31}$")]
    private static partial Regex UsernameShape();
}
