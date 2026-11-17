using Dalan.Admin.Contracts;
using Dalan.Identity.Data;
using Dalan.Identity.Sessions;
using Dalan.Identity.Users;
using Dalan.Notification.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dalan.Identity.SmsLogin;

/// <summary>Login by SMS code: challenge (SEC-06), code (AUTH-11), check (AUTH-12).</summary>
internal static class SmsLoginEndpoints
{
    private sealed record ChallengeRequest(string? Phone);

    private sealed record SmsRequest(string? Phone, string? Challenge, string? Nonce);

    private sealed record VerifyRequest(string? Phone, string? Code, string? Name);

    public static void MapSmsLogin(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/auth/challenge", ChallengeAsync);
        endpoints.MapPost("/api/auth/sms", SendAsync);
        endpoints.MapPost("/api/auth/sms/verify", VerifyAsync);
    }

    private static IResult BadPhone() => AuthJson.BadRequest("bad_phone",
        """Send JSON with "phone": an Iranian mobile number like 09121234567.""");

    private static IResult WrongCode() => AuthJson.BadRequest("wrong_code",
        "The code is wrong, too old, already used, or had too many tries. Ask for a new code.");

    private static async Task<IResult> ChallengeAsync(HttpContext http, ProofOfWork proofOfWork)
    {
        var request = await AuthJson.ReadAsync<ChallengeRequest>(http);
        return PhoneNumber.Normalize(request?.Phone) is { } phone
            ? AuthJson.Ok(new { Challenge = proofOfWork.NewChallenge(phone), proofOfWork.Difficulty })
            : BadPhone();
    }

    private static async Task<IResult> SendAsync(
        HttpContext http, ProofOfWork proofOfWork, SmsCodes codes, ISmsSender sender, IAccessLog accessLog,
        IOptionsMonitor<IdentityOptions> options)
    {
        var request = await AuthJson.ReadAsync<SmsRequest>(http);
        if (PhoneNumber.Normalize(request?.Phone) is not { } phone) return BadPhone();

        if (!proofOfWork.Check(phone, request!.Challenge, request.Nonce))
        {
            return AuthJson.BadRequest("bad_proof",
                "The proof of work is missing, wrong or too old. Get a new challenge from POST /api/auth/challenge and solve it.");
        }

        var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var (outcome, wait) = await codes.SendAsync(phone, ip, sender, http.RequestAborted);

        // ADM-04: the person who gets the SMS may not be the one who asked for it.
        if (outcome == SmsOutcome.Sent)
            await accessLog.RecordSmsAsync(phone, http.Connection.RemoteIpAddress, http.Connection.RemotePort, http.RequestAborted);

        var settings = options.CurrentValue;
        return outcome switch
        {
            SmsOutcome.Sent => AuthJson.Ok(new
            {
                ExpiresIn = settings.SmsCodeSeconds,
                RetryIn = settings.SmsWaitSeconds,
                CodeDigits = settings.SmsCodeDigits,
            }),
            SmsOutcome.Wait => AuthJson.TooManyRequests(http, "sms_wait", wait,
                $"A code was just sent to this number. Wait {AuthJson.Seconds(wait)} second(s) before asking again."),
            SmsOutcome.Limit => AuthJson.Error(StatusCodes.Status429TooManyRequests, "sms_limit",
                "Too many codes were sent today. Log in with your password, or try again tomorrow."),
            _ => AuthJson.Error(StatusCodes.Status503ServiceUnavailable, "sms_unavailable",
                "The SMS could not be sent now. Try again in a few minutes, or log in with your password."),
        };
    }

    private static async Task<IResult> VerifyAsync(
        HttpContext http,
        SmsCodes codes,
        SessionTokens sessions,
        SecurityLog log,
        IDbContextFactory<IdentityDb> dbFactory,
        IOptionsMonitor<IdentityOptions> options,
        TimeProvider time)
    {
        var request = await AuthJson.ReadAsync<VerifyRequest>(http);
        if (PhoneNumber.Normalize(request?.Phone) is not { } phone) return BadPhone();

        var (check, codeId) = await codes.CheckAsync(phone, request!.Code?.Trim() ?? "", http.RequestAborted);
        if (check == CodeCheck.Wrong)
        {
            log.LoginFailed(SessionTokens.BySms, "wrong code");
            return WrongCode();
        }

        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var isNew = !await db.Users.AnyAsync(u => u.Phone == phone, http.RequestAborted);
        var name = request.Name?.Trim() ?? "";
        if (isNew && (name.Length == 0 || name.Length > options.CurrentValue.MaxNameLength))
        {
            await codes.GiveBackTryAsync(codeId, http.RequestAborted);
            return AuthJson.BadRequest("name_required",
                $"""This number is new. Send the same call again with "name": 1 to {options.CurrentValue.MaxNameLength} characters.""");
        }

        if (!await codes.UseAsync(codeId, http.RequestAborted)) return WrongCode();

        // A known user keeps the name: the insert changes nothing when the number exists.
        var now = time.GetUtcNow();
        var ids = await db.Database.SqlQuery<long>($"""
            INSERT INTO identity.users (phone, display_name, created_at)
            VALUES ({phone}, {name}, {now})
            ON CONFLICT (phone) DO UPDATE SET phone = EXCLUDED.phone
            RETURNING id AS "Value"
            """).ToListAsync(http.RequestAborted);

        await sessions.SignInAsync(http, ids[0], SessionTokens.BySms);
        return AuthJson.Ok(await SessionEndpoints.ProfileAsync(dbFactory, ids[0], http.RequestAborted));
    }
}
