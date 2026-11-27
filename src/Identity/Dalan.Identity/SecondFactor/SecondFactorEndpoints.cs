using System.Globalization;
using Dalan.Admin.Contracts;
using Dalan.Identity.Data;
using Dalan.Identity.Sessions;
using Dalan.Identity.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QRCoder;

namespace Dalan.Identity.SecondFactor;

/// <summary>AUTH-37: turn the second factor on and off, and the login step that asks for its code. SEC-15 for admins.</summary>
internal static class SecondFactorEndpoints
{
    private sealed record CodeRequest(string? Code);

    private sealed record LoginRequest(string? Ticket, string? Code);

    public static void MapSecondFactor(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/account/totp", StartAsync).RequireAuthorization();
        endpoints.MapPost("/api/account/totp/confirm", ConfirmAsync).RequireAuthorization();
        endpoints.MapPost("/api/account/totp/off", OffAsync).RequireAuthorization();
        endpoints.MapPost("/api/auth/totp", LoginAsync);
    }

    /// <summary>The answer of a login that must also give a code: no session yet.</summary>
    public static IResult Ask(string ticket) => AuthJson.Ok(new { TotpRequired = true, Ticket = ticket });

    /// <summary>SEC-15.</summary>
    public static IResult AdminNeeds2fa(string message) => AuthJson.Error(StatusCodes.Status403Forbidden, "admin_needs_2fa", message);

    private static IResult WrongCode() => AuthJson.BadRequest("wrong_code",
        "The code is wrong, too old or already used. Send the 6 digits your authenticator app shows now, or an unused recovery code.");

    private static async Task<IResult> StartAsync(
        HttpContext http, IDbContextFactory<IdentityDb> dbFactory, SecondFactors factors, UserAdmin users, IAccessLog accessLog,
        IOptionsMonitor<IdentityOptions> options)
    {
        var userId = SessionTokens.UserId(http.User);
        // ADM-01: this door knows its caller.
        await accessLog.RecordAsync(AccessActor.User, userId, http.Connection.RemoteIpAddress, http.Connection.RemotePort, http.RequestAborted);
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new { u.Phone, u.Email, u.Username, u.DisplayName, HasPassword = u.PasswordHash != null })
            .FirstOrDefaultAsync(http.RequestAborted);
        if (user is null) return AuthJson.Unauthorized();
        if (!user.HasPassword && !users.IsAdminPhone(user.Phone))
        {
            return AuthJson.Error(StatusCodes.Status403Forbidden, "totp_not_allowed",
                "Set a password first. The second factor guards the password login.");
        }

        if (await SecondFactors.IsOnAsync(db, userId, http.RequestAborted))
        {
            return AuthJson.Error(StatusCodes.Status409Conflict, "totp_on",
                "The second factor is on already. Turn it off first to set up a new one.");
        }

        var secret = await factors.StartAsync(userId, http.RequestAborted);
        var step = options.CurrentValue.TotpStepSeconds;
        var url = $"otpauth://totp/Dalan:{Uri.EscapeDataString(user.Username ?? user.Email ?? user.DisplayName)}?secret={secret}&issuer=Dalan"
            + (step == 30 ? "" : string.Create(CultureInfo.InvariantCulture, $"&period={step}"));
        return AuthJson.Ok(new { Secret = secret, OtpauthUrl = url, QrSvg = QrSvg(url) });
    }

    private static async Task<IResult> ConfirmAsync(
        HttpContext http, SecondFactors factors, SecurityLog log, IAuditLog audit, IAccessLog accessLog)
    {
        var userId = SessionTokens.UserId(http.User);
        await accessLog.RecordAsync(AccessActor.User, userId, http.Connection.RemoteIpAddress, http.Connection.RemotePort, http.RequestAborted);
        var code = (await AuthJson.ReadAsync<CodeRequest>(http))?.Code?.Trim() ?? "";
        if (code.Length != Totp.Digits || await factors.ConfirmAsync(userId, code, http.RequestAborted) is not { } recoveryCodes)
            return WrongCode();

        log.SecondFactor(userId, on: true);
        await audit.RecordAsync(userId, "totp.on", $"user {userId}", null, http.RequestAborted);
        return AuthJson.Ok(new { RecoveryCodes = recoveryCodes });
    }

    private static async Task<IResult> OffAsync(
        HttpContext http, SecondFactors factors, UserAdmin users, SecurityLog log, IAuditLog audit, IAccessLog accessLog)
    {
        var userId = SessionTokens.UserId(http.User);
        await accessLog.RecordAsync(AccessActor.User, userId, http.Connection.RemoteIpAddress, http.Connection.RemotePort, http.RequestAborted);
        // SEC-15: an admin keeps it on.
        if ((await users.GetAccessAsync(userId, http.RequestAborted)).IsAdmin)
            return AdminNeeds2fa("An admin of Dalan must keep the second factor on.");

        var code = (await AuthJson.ReadAsync<CodeRequest>(http))?.Code;
        if (!await factors.CheckAsync(userId, code, http.RequestAborted)) return WrongCode();

        await factors.OffAsync(userId, http.RequestAborted);
        log.SecondFactor(userId, on: false);
        await audit.RecordAsync(userId, "totp.off", $"user {userId}", null, http.RequestAborted);
        return Results.NoContent();
    }

    /// <summary>The second step of a login: the ticket and a code make the session, with the method of the first step.</summary>
    private static async Task<IResult> LoginAsync(
        HttpContext http, SecondFactors factors, SessionTokens sessions, UserAdmin users, SecurityLog log, IAccessLog accessLog)
    {
        var request = await AuthJson.ReadAsync<LoginRequest>(http);
        var ticket = request?.Ticket ?? "";
        var code = request?.Code?.Trim() ?? "";
        if (ticket.Length == 0 || code.Length == 0)
            return AuthJson.BadRequest("bad_request", """Send JSON like {"ticket": "...", "code": "123456"}.""");

        if (await factors.TryTicketAsync(ticket, http.RequestAborted) is not { } pending)
        {
            log.LoginFailed("totp", "ticket ended or too many tries");
            return AuthJson.Unauthorized();
        }

        var (userId, method, lastTry) = pending;
        if (!await factors.CheckAsync(userId, code, http.RequestAborted))
        {
            log.LoginFailed("totp", "wrong code");
            if (!lastTry) return WrongCode();
            await factors.DropTicketAsync(ticket, http.RequestAborted);
            return AuthJson.Unauthorized();
        }

        await factors.DropTicketAsync(ticket, http.RequestAborted);
        if ((await users.GetAccessAsync(userId, http.RequestAborted)).IsSuspended) return AuthJson.Suspended();

        // ADM-01: this door knows its caller.
        await accessLog.RecordAsync(AccessActor.User, userId, http.Connection.RemoteIpAddress, http.Connection.RemotePort, http.RequestAborted);
        await sessions.SignInAsync(http, userId, method);
        return AuthJson.Ok(await users.ProfileAsync(userId, http.RequestAborted));
    }

    /// <summary>The QR code as SVG text, made here: nothing is loaded from another host (rule 7).</summary>
    private static string QrSvg(string text)
    {
        using var data = QRCodeGenerator.GenerateQrCode(text, QRCodeGenerator.ECCLevel.M);
        return new SvgQRCode(data).GetGraphic(4, "#000000", "#ffffff", true, SvgQRCode.SizingMode.ViewBoxAttribute);
    }
}
