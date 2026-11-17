namespace Dalan.Admin.Contracts;

/// <summary>ADM-09: who changed what and when. Never pass a token or a password.</summary>
public interface IAuditLog
{
    /// <param name="userId">The admin or builder who did it.</param>
    /// <param name="action">A short stable name, like "bot.create" or "setting.set".</param>
    /// <param name="target">What it was done to, like "bot 5" or "Relay:MaxTextLength".</param>
    /// <param name="detail">Extra facts, for example the old and new value; null when there are none.</param>
    Task RecordAsync(long userId, string action, string target, string? detail, CancellationToken cancellationToken);
}
