using System.Globalization;
using Dalan.Admin.Contracts;
using Dalan.Identity.Codes;
using Dalan.Identity.Contracts;
using Dalan.Identity.Data;
using Dalan.Identity.Passwords;
using Dalan.Identity.Sessions;
using Dalan.Identity.Users;
using Dalan.Notification.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dalan.Identity.Account;

/// <summary>
/// AUTH-36: a user deletes the own account with the password (or, with no password, a code to the own phone, else email)
/// and a confirm word. Refused while the user owns a bot or a site, and for an admin. The access log and the sent-code
/// records are in other tables and stay.
/// </summary>
internal static class DeleteAccountEndpoints
{
    private const string SmsChannel = "sms";
    private const string EmailChannel = "email";

    private sealed record CodeRequest(string? Challenge, string? Nonce);

    private sealed record DeleteRequest(string? Password, string? Code, string? Word);

    /// <summary>What the delete needs to know of the user.</summary>
    private sealed record Account(long Id, string? Phone, string? Email, string? PasswordHash)
    {
        /// <summary>Where a code goes: the phone, else the email (every user has one of the two).</summary>
        public (string Channel, string Target) CodeTarget => Phone is not null ? (SmsChannel, Phone) : (EmailChannel, Email!);
    }

    public static void MapDeleteAccount(this IEndpointRouteBuilder endpoints)
    {
        var delete = endpoints.MapGroup("/api/account/delete").RequireAuthorization().AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            await http.RequestServices.GetRequiredService<IAccessLog>().RecordAsync(AccessActor.User,
                SessionTokens.UserId(http.User), http.Connection.RemoteIpAddress, http.Connection.RemotePort, http.RequestAborted);
            return await next(context);
        });
        delete.MapPost("/challenge", ChallengeAsync);
        delete.MapPost("/code", SendCodeAsync);
        delete.MapPost("", DeleteAsync);
    }

    private static async Task<IResult> ChallengeAsync(
        HttpContext http, IDbContextFactory<IdentityDb> dbFactory, UserAdmin users, ProofOfWork proofOfWork)
    {
        var (account, refusal) = await AccountAsync(http, dbFactory, users);
        if (account is null) return refusal!;

        var (channel, target) = account.CodeTarget;
        return AuthJson.Ok(new { Challenge = proofOfWork.NewChallenge(target), proofOfWork.Difficulty, Channel = channel });
    }

    private static async Task<IResult> SendCodeAsync(
        HttpContext http, IDbContextFactory<IdentityDb> dbFactory, UserAdmin users, ProofOfWork proofOfWork, SmsCodes smsCodes,
        EmailCodes emailCodes, ISmsSender sms, IEmailSender email, IAccessLog accessLog)
    {
        var (account, refusal) = await AccountAsync(http, dbFactory, users);
        if (account is null) return refusal!;

        var request = await AuthJson.ReadAsync<CodeRequest>(http);
        var (channel, target) = account.CodeTarget;
        if (!proofOfWork.Check(target, request?.Challenge, request?.Nonce)) return AuthJson.BadProof();

        var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var (outcome, wait) = channel == SmsChannel
            ? await smsCodes.SendAsync(target, ip, (code, cancel) => sms.SendLoginCodeAsync(target, code, cancel), http.RequestAborted)
            : await emailCodes.SendAsync(target, ip, (code, cancel) => email.SendLoginCodeAsync(target, code, cancel), http.RequestAborted);

        // Rule 13, ADM-04, ADM-16: every sent code is recorded with its phone or email.
        if (outcome == CodeOutcome.Sent)
        {
            if (channel == SmsChannel) await accessLog.RecordSmsAsync(target, http.Connection.RemoteIpAddress, http.Connection.RemotePort, http.RequestAborted);
            else await accessLog.RecordEmailAsync(target, http.Connection.RemoteIpAddress, http.Connection.RemotePort, http.RequestAborted);
        }

        return outcome switch
        {
            CodeOutcome.Sent => Results.NoContent(),
            CodeOutcome.Wait => AuthJson.TooManyRequests(http, $"{channel}_wait", wait,
                $"A code was just sent. Wait {AuthJson.Seconds(wait)} second(s) before asking again."),
            CodeOutcome.Limit => AuthJson.TooManyRequests(http, $"{channel}_limit", wait,
                $"Too many codes were asked from this address. Wait {AuthJson.Seconds(wait)} second(s)."),
            _ => AuthJson.Error(StatusCodes.Status503ServiceUnavailable, $"{channel}_unavailable",
                "The code could not be sent now. Try again in a few minutes."),
        };
    }

    private static async Task<IResult> DeleteAsync(
        HttpContext http,
        IDbContextFactory<IdentityDb> dbFactory,
        UserAdmin users,
        IEnumerable<IUserHoldings> holdings,
        PasswordHasher hasher,
        PasswordTries tries,
        SmsCodes smsCodes,
        EmailCodes emailCodes,
        SuspendedUsers stopped,
        IAuditLog audit,
        SecurityLog log,
        IOptionsMonitor<IdentityOptions> options)
    {
        var (account, refusal) = await AccountAsync(http, dbFactory, users);
        if (account is null) return refusal!;

        // Bots and sites (and live demos) would be left without an owner. Chats are not products.
        var held = new UserHoldings();
        foreach (var service in holdings) held += await service.CountAsync(account.Id, http.RequestAborted);
        if (held.Bots + held.Sites > 0)
        {
            return AuthJson.Error(StatusCodes.Status409Conflict, "owns_things",
                $"You still own {held.Bots} bot(s) and {held.Sites} site(s). Delete them first, then the account.");
        }

        var settings = options.CurrentValue;
        var request = await AuthJson.ReadAsync<DeleteRequest>(http);
        var word = request?.Word?.Trim() ?? "";
        var words = SessionEndpoints.DeleteWords(settings);
        if (word.Length == 0 || !words.Contains(word, StringComparer.OrdinalIgnoreCase))
        {
            return AuthJson.BadRequest("bad_word",
                $"""Send "word" with one of these words to confirm: {string.Join(", ", words)}.""");
        }

        if (account.PasswordHash is { } stored)
        {
            var address = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (tries.Check(string.Create(CultureInfo.InvariantCulture, $"user {account.Id}"), address) is { } wait)
            {
                return AuthJson.TooManyRequests(http, "rate_limited", wait,
                    $"Too many password tries. Wait {AuthJson.Seconds(wait)} second(s).");
            }

            var password = request?.Password ?? "";
            if (password.Length == 0 || password.Length > settings.PasswordMaxLength || !await hasher.VerifyAsync(stored, password))
                return WrongProof("""Send your "password".""");
        }
        else
        {
            var (channel, target) = account.CodeTarget;
            var code = request?.Code?.Trim() ?? "";
            var (check, codeId) = channel == SmsChannel
                ? await smsCodes.CheckAsync(target, code, http.RequestAborted)
                : await emailCodes.CheckAsync(target, code, http.RequestAborted);
            var used = check == CodeCheck.Right && (channel == SmsChannel
                ? await smsCodes.UseAsync(codeId, http.RequestAborted)
                : await emailCodes.UseAsync(codeId, http.RequestAborted));
            if (!used) return WrongProof("""Send the "code" from POST /api/account/delete/code. It may be wrong, too old or used.""");
        }

        // Sessions, keys, the second factor and pending merges go with the row (ON DELETE CASCADE).
        await using (var db = await dbFactory.CreateDbContextAsync(http.RequestAborted))
            await db.Users.Where(u => u.Id == account.Id).ExecuteDeleteAsync(http.RequestAborted);

        // The session tokens that have not expired stop at once, like a suspended user's. Ids are never used again.
        stopped.Set(account.Id, true);
        SessionTokens.DeleteCookies(http);
        log.AccountDeleted(account.Id);
        await audit.RecordAsync(account.Id, "account.delete", string.Create(CultureInfo.InvariantCulture, $"user {account.Id}"), null,
            http.RequestAborted);
        return Results.NoContent();
    }

    private static IResult WrongProof(string message) => AuthJson.BadRequest("bad_proof", message);

    /// <summary>The caller's account, or the answer when there is none or it is an admin's.</summary>
    private static async Task<(Account? Account, IResult? Refusal)> AccountAsync(
        HttpContext http, IDbContextFactory<IdentityDb> dbFactory, UserAdmin users)
    {
        var userId = SessionTokens.UserId(http.User);
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var account = await db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new Account(u.Id, u.Phone, u.Email, u.PasswordHash))
            .FirstOrDefaultAsync(http.RequestAborted);
        if (account is null) return (null, AuthJson.Unauthorized());
        // Admins come only from the server's environment (AUTH-18); deleting the row would not remove the admin.
        if (users.IsAdminPhone(account.Phone))
        {
            return (null, AuthJson.Error(StatusCodes.Status403Forbidden, "admin_delete",
                "An admin account cannot be deleted here. Admins come from the server's settings (Identity:AdminPhones)."));
        }

        return (account, null);
    }
}
