using Dalan.Admin.Contracts;
using Dalan.Identity.Codes;
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

/// <summary>Login by SMS code: code (AUTH-11), check (AUTH-12). The challenge (SEC-06) is in <see cref="ChallengeEndpoint"/>.</summary>
internal static class SmsLoginEndpoints
{
    private sealed record SmsRequest(string? Phone, string? Challenge, string? Nonce);

    private sealed record VerifyRequest(string? Phone, string? Code, string? Name);

    public static void MapSmsLogin(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/auth/sms", SendAsync);
        endpoints.MapPost("/api/auth/sms/verify", VerifyAsync);
    }

    public static IResult BadPhone() => AuthJson.BadRequest("bad_phone",
        """Send JSON with "phone": a mobile number like 09121234567.""");

    public static IResult WrongCode() => AuthJson.BadRequest("wrong_code",
        "The code is wrong, too old, already used, or had too many tries. Ask for a new code.");

    private static async Task<IResult> SendAsync(
        HttpContext http, ProofOfWork proofOfWork, SmsCodes codes, ISmsSender sender, IAccessLog accessLog,
        IDbContextFactory<IdentityDb> dbFactory, IOptionsMonitor<IdentityOptions> options, UserAdmin users)
    {
        var request = await AuthJson.ReadAsync<SmsRequest>(http);
        if (PhoneNumber.Normalize(request?.Phone) is not { } phone) return BadPhone();

        if (!proofOfWork.Check(phone, request!.Challenge, request.Nonce)) return AuthJson.BadProof();

        // AUTH-20: no SMS for a suspended user; it would cost money and lead nowhere.
        await using (var db = await dbFactory.CreateDbContextAsync(http.RequestAborted))
        {
            if (await db.Users.AnyAsync(u => u.Phone == phone && u.SuspendedAt != null, http.RequestAborted))
                return AuthJson.Suspended();
        }

        var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var (outcome, wait) = await codes.SendAsync(
            phone, ip, (code, cancellationToken) => sender.SendLoginCodeAsync(phone, code, cancellationToken), http.RequestAborted,
            noCap: users.IsAdminPhone(phone));

        // ADM-04: the person who gets the SMS may not be the one who asked for it.
        if (outcome == CodeOutcome.Sent)
            await accessLog.RecordSmsAsync(phone, http.Connection.RemoteIpAddress, http.Connection.RemotePort, http.RequestAborted);

        var settings = options.CurrentValue;
        return outcome switch
        {
            CodeOutcome.Sent => AuthJson.Ok(new
            {
                ExpiresIn = settings.SmsCodeSeconds,
                RetryIn = AuthJson.Seconds(wait),
                CodeDigits = settings.SmsCodeDigits,
            }),
            CodeOutcome.Wait => AuthJson.TooManyRequests(http, "sms_wait", wait,
                $"A code was just sent to this number. Wait {AuthJson.Seconds(wait)} second(s) before asking again."),
            CodeOutcome.Limit => AuthJson.TooManyRequests(http, "sms_limit", wait,
                $"Too many codes were asked from this address. Wait {AuthJson.Seconds(wait)} second(s), or log in with your password."),
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
        UserAdmin users,
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

        if ((await users.GetAccessAsync(ids[0], http.RequestAborted)).IsSuspended) return AuthJson.Suspended();

        await sessions.SignInAsync(http, ids[0], SessionTokens.BySms);
        return AuthJson.Ok(await users.ProfileAsync(ids[0], http.RequestAborted));
    }
}
