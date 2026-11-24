using Dalan.Admin.Contracts;
using Dalan.Identity.Codes;
using Dalan.Identity.Data;
using Dalan.Identity.Sessions;
using Dalan.Identity.SmsLogin;
using Dalan.Identity.Users;
using Dalan.Notification.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dalan.Identity.EmailLogin;

/// <summary>Level 1 login by email code: code (AUTH-24), check (AUTH-25). The challenge (SEC-06) is in <see cref="ChallengeEndpoint"/>.</summary>
internal static class EmailLoginEndpoints
{
    private sealed record EmailRequest(string? Email, string? Challenge, string? Nonce);

    private sealed record VerifyRequest(string? Email, string? Code, string? Name);

    public static void MapEmailLogin(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/auth/email", SendAsync);
        endpoints.MapPost("/api/auth/email/verify", VerifyAsync);
    }

    public static IResult BadEmail() => AuthJson.BadRequest("bad_email",
        """Send JSON with "email": an address like sara@example.com.""");

    private static async Task<IResult> SendAsync(
        HttpContext http, ProofOfWork proofOfWork, EmailCodes codes, IEmailSender sender, IAccessLog accessLog,
        IDbContextFactory<IdentityDb> dbFactory, IOptionsMonitor<IdentityOptions> options)
    {
        var request = await AuthJson.ReadAsync<EmailRequest>(http);
        if (EmailAddress.Normalize(request?.Email) is not { } email) return BadEmail();
        if (!proofOfWork.Check(email, request!.Challenge, request.Nonce)) return AuthJson.BadProof();

        // AUTH-20: no email for a suspended user.
        await using (var db = await dbFactory.CreateDbContextAsync(http.RequestAborted))
        {
            if (await db.Users.AnyAsync(u => u.Email == email && u.SuspendedAt != null, http.RequestAborted))
                return AuthJson.Suspended();
        }

        var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var (outcome, wait) = await codes.SendAsync(
            email, ip, (code, cancellationToken) => sender.SendLoginCodeAsync(email, code, cancellationToken), http.RequestAborted);

        // ADM-16: like ADM-04 for SMS.
        if (outcome == CodeOutcome.Sent)
            await accessLog.RecordEmailAsync(email, http.Connection.RemoteIpAddress, http.Connection.RemotePort, http.RequestAborted);

        var settings = options.CurrentValue;
        return outcome switch
        {
            CodeOutcome.Sent => AuthJson.Ok(new
            {
                ExpiresIn = settings.EmailCodeSeconds,
                RetryIn = AuthJson.Seconds(wait),
                CodeDigits = settings.EmailCodeDigits,
            }),
            CodeOutcome.Wait => AuthJson.TooManyRequests(http, "email_wait", wait,
                $"A code was just sent to this address. Wait {AuthJson.Seconds(wait)} second(s) before asking again."),
            CodeOutcome.Limit => AuthJson.TooManyRequests(http, "email_limit", wait,
                $"Too many codes were asked from this address. Wait {AuthJson.Seconds(wait)} second(s), or log in with your password."),
            _ => AuthJson.Error(StatusCodes.Status503ServiceUnavailable, "email_unavailable",
                "The email could not be sent now. Try again in a few minutes, or log in another way."),
        };
    }

    private static async Task<IResult> VerifyAsync(
        HttpContext http,
        EmailCodes codes,
        SessionTokens sessions,
        SecurityLog log,
        IDbContextFactory<IdentityDb> dbFactory,
        IOptionsMonitor<IdentityOptions> options,
        UserAdmin users,
        TimeProvider time)
    {
        var request = await AuthJson.ReadAsync<VerifyRequest>(http);
        if (EmailAddress.Normalize(request?.Email) is not { } email) return BadEmail();

        var (check, codeId) = await codes.CheckAsync(email, request!.Code?.Trim() ?? "", http.RequestAborted);
        if (check == CodeCheck.Wrong)
        {
            log.LoginFailed(SessionTokens.ByEmail, "wrong code");
            return SmsLoginEndpoints.WrongCode();
        }

        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var isNew = !await db.Users.AnyAsync(u => u.Email == email, http.RequestAborted);
        var name = request.Name?.Trim() ?? "";
        if (isNew && (name.Length == 0 || name.Length > options.CurrentValue.MaxNameLength))
        {
            await codes.GiveBackTryAsync(codeId, http.RequestAborted);
            return AuthJson.BadRequest("name_required",
                $"""This address is new. Send the same call again with "name": 1 to {options.CurrentValue.MaxNameLength} characters.""");
        }

        if (!await codes.UseAsync(codeId, http.RequestAborted)) return SmsLoginEndpoints.WrongCode();

        // A known user keeps the name: the insert changes nothing when the address exists.
        var now = time.GetUtcNow();
        var ids = await db.Database.SqlQuery<long>($"""
            INSERT INTO identity.users (email, display_name, created_at)
            VALUES ({email}, {name}, {now})
            ON CONFLICT (email) DO UPDATE SET email = EXCLUDED.email
            RETURNING id AS "Value"
            """).ToListAsync(http.RequestAborted);

        if ((await users.GetAccessAsync(ids[0], http.RequestAborted)).IsSuspended) return AuthJson.Suspended();

        await sessions.SignInAsync(http, ids[0], SessionTokens.ByEmail);
        return AuthJson.Ok(await users.ProfileAsync(ids[0], http.RequestAborted));
    }
}
