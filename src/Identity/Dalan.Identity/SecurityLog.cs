using Microsoft.Extensions.Logging;

namespace Dalan.Identity;

/// <summary>
/// AUTH-17: one log line for each security event. Never a code, a password, a token or a whole phone number.
/// The audit table comes with the Admin service; until then the log is the record.
/// </summary>
internal sealed partial class SecurityLog(ILogger<SecurityLog> logger)
{
    public void Login(long userId, string method) => LogLogin(logger, userId, method);

    public void LoginFailed(string method, string reason) => LogLoginFailed(logger, method, reason);

    public void SmsSent(string phone) => LogSmsSent(logger, Mask(phone));

    public void SmsRefused(string phone, string reason) => LogSmsRefused(logger, Mask(phone), reason);

    public void SmsBudgetUsedUp(int budget) => LogSmsBudget(logger, budget);

    public void RefreshReuse() => LogRefreshReuse(logger);

    public void PasswordSet(long userId) => LogPasswordSet(logger, userId);

    /// <summary>+989121234567 becomes +98912***4567.</summary>
    private static string Mask(string phone) => phone.Length < 10 ? "***" : $"{phone[..6]}***{phone[^4..]}";

    [LoggerMessage(LogLevel.Information, "Security event login: user {UserId} by {Method}")]
    private static partial void LogLogin(ILogger logger, long userId, string method);

    [LoggerMessage(LogLevel.Warning, "Security event login_failed: by {Method}, {Reason}")]
    private static partial void LogLoginFailed(ILogger logger, string method, string reason);

    [LoggerMessage(LogLevel.Information, "Security event sms_sent: to {Phone}")]
    private static partial void LogSmsSent(ILogger logger, string phone);

    [LoggerMessage(LogLevel.Warning, "Security event sms_refused: to {Phone}, {Reason}")]
    private static partial void LogSmsRefused(ILogger logger, string phone, string reason);

    [LoggerMessage(LogLevel.Error,
        "Security event sms_refused: the SMS budget of {Budget} per 24 hours (Identity:SmsPerDay) is used up. Nobody can log in by SMS until it frees up.")]
    private static partial void LogSmsBudget(ILogger logger, int budget);

    [LoggerMessage(LogLevel.Warning, "Security event refresh_reuse: an old refresh token came back; that device's session was ended")]
    private static partial void LogRefreshReuse(ILogger logger);

    [LoggerMessage(LogLevel.Information, "Security event password_set: user {UserId}")]
    private static partial void LogPasswordSet(ILogger logger, long userId);
}
