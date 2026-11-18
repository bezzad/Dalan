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

    /// <summary>The event is "sms_sent" or "email_sent".</summary>
    public void CodeSent(string channel, string target) => LogCodeSent(logger, $"{channel}_sent", Mask(target));

    public void CodeRefused(string channel, string target, string reason) => LogCodeRefused(logger, $"{channel}_refused", Mask(target), reason);

    public void BudgetUsedUp(string channel, int budget, string setting) => LogBudget(logger, $"{channel}_refused", channel, budget, setting);

    public void PhoneAdded(long userId) => LogPhoneAdded(logger, userId);

    public void RefreshReuse() => LogRefreshReuse(logger);

    public void PasswordSet(long userId) => LogPasswordSet(logger, userId);

    /// <summary>+989121234567 becomes +98912***4567; sara@example.com becomes sa***@example.com.</summary>
    private static string Mask(string target) => target.IndexOf('@', StringComparison.Ordinal) switch
    {
        > 0 and var at => $"{target[..Math.Min(2, at)]}***{target[at..]}",
        _ when target.Length < 10 => "***",
        _ => $"{target[..6]}***{target[^4..]}",
    };

    [LoggerMessage(LogLevel.Information, "Security event login: user {UserId} by {Method}")]
    private static partial void LogLogin(ILogger logger, long userId, string method);

    [LoggerMessage(LogLevel.Warning, "Security event login_failed: by {Method}, {Reason}")]
    private static partial void LogLoginFailed(ILogger logger, string method, string reason);

    [LoggerMessage(LogLevel.Information, "Security event {Event}: to {Target}")]
    private static partial void LogCodeSent(ILogger logger, string @event, string target);

    [LoggerMessage(LogLevel.Warning, "Security event {Event}: to {Target}, {Reason}")]
    private static partial void LogCodeRefused(ILogger logger, string @event, string target, string reason);

    [LoggerMessage(LogLevel.Error,
        "Security event {Event}: the {Channel} budget of {Budget} per 24 hours ({Setting}) is used up. Nobody can log in by {Channel} until it frees up.")]
    private static partial void LogBudget(ILogger logger, string @event, string channel, int budget, string setting);

    [LoggerMessage(LogLevel.Information, "Security event phone_added: user {UserId}")]
    private static partial void LogPhoneAdded(ILogger logger, long userId);

    [LoggerMessage(LogLevel.Warning, "Security event refresh_reuse: an old refresh token came back; that device's session was ended")]
    private static partial void LogRefreshReuse(ILogger logger);

    [LoggerMessage(LogLevel.Information, "Security event password_set: user {UserId}")]
    private static partial void LogPasswordSet(ILogger logger, long userId);
}
