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

    /// <summary>AUTH-30: an email or GitHub account was added to a user.</summary>
    public void WayLinked(long userId, string way) => LogWayLinked(logger, userId, way);

    /// <summary>AUTH-31: the proof is done and the merge waits for the person.</summary>
    public void MergeOffered(long userId, long otherUserId, string way) => LogMergeOffered(logger, userId, otherUserId, way);

    /// <summary>AUTH-34.</summary>
    public void Merged(long fromUserId, long intoUserId, string way) => LogMerged(logger, fromUserId, intoUserId, way);

    /// <summary>AUTH-34: a service could not move what it holds yet; it is tried again.</summary>
    public void MergeFollowUpFailed(long mergeId, Exception exception) => LogMergeFollowUpFailed(logger, mergeId, exception);

    public void RefreshReuse() => LogRefreshReuse(logger);

    public void PasswordSet(long userId) => LogPasswordSet(logger, userId);

    /// <summary>AUTH-37: the second factor was turned on or off.</summary>
    public void SecondFactor(long userId, bool on) => LogSecondFactor(logger, on ? "totp_on" : "totp_off", userId);

    /// <summary>AUTH-35: the user ended sessions from the session list.</summary>
    public void SessionsEnded(long userId, int count) => LogSessionsEnded(logger, userId, count);

    /// <summary>AUTH-36.</summary>
    public void AccountDeleted(long userId) => LogAccountDeleted(logger, userId);

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

    [LoggerMessage(LogLevel.Information, "Security event way_linked: user {UserId} by {Way}")]
    private static partial void LogWayLinked(ILogger logger, long userId, string way);

    [LoggerMessage(LogLevel.Information, "Security event merge_offered: user {UserId} proved the {Way} of user {OtherUserId}")]
    private static partial void LogMergeOffered(ILogger logger, long userId, long otherUserId, string way);

    [LoggerMessage(LogLevel.Information, "Security event merged: user {FromUserId} merged into user {IntoUserId} by {Way}")]
    private static partial void LogMerged(ILogger logger, long fromUserId, long intoUserId, string way);

    [LoggerMessage(LogLevel.Error, "Security event merge_follow_up_failed: merge {MergeId} is tried again later")]
    private static partial void LogMergeFollowUpFailed(ILogger logger, long mergeId, Exception exception);

    [LoggerMessage(LogLevel.Information, "Security event phone_added: user {UserId}")]
    private static partial void LogPhoneAdded(ILogger logger, long userId);

    [LoggerMessage(LogLevel.Warning, "Security event refresh_reuse: an old refresh token came back; that device's session was ended")]
    private static partial void LogRefreshReuse(ILogger logger);

    [LoggerMessage(LogLevel.Information, "Security event password_set: user {UserId}")]
    private static partial void LogPasswordSet(ILogger logger, long userId);

    [LoggerMessage(LogLevel.Information, "Security event {Event}: user {UserId}")]
    private static partial void LogSecondFactor(ILogger logger, string @event, long userId);

    [LoggerMessage(LogLevel.Information, "Security event sessions_ended: user {UserId} ended {Count} session(s)")]
    private static partial void LogSessionsEnded(ILogger logger, long userId, int count);

    [LoggerMessage(LogLevel.Information, "Security event account_deleted: user {UserId}")]
    private static partial void LogAccountDeleted(ILogger logger, long userId);
}
