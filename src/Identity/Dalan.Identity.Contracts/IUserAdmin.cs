namespace Dalan.Identity.Contracts;

/// <summary>AUTH-19: may a user create bots?</summary>
public enum BuilderState
{
    None = 0,
    Requested = 1,
    Approved = 2,

    /// <summary>AUTH-19: the admin said no. Auto-approval (AUTH-39) never changes it; a new request waits for the admin.</summary>
    Refused = 3,
}

/// <summary>What a user may do now. A user that does not exist has every flag false.</summary>
public sealed record UserAccess(bool IsAdmin, bool IsBuilder, bool IsSuspended)
{
    /// <summary>AUTH-37: the user has the second factor on. Read only for an admin (SEC-15); false for others.</summary>
    public bool HasSecondFactor { get; init; }
}

/// <summary>A user as the admin sees it: with the phone number and email, for legal complaints (ADM-11).</summary>
public sealed record AdminUser(
    long Id,
    string? Phone,
    string? Email,
    string Name,
    string? Username,
    DateTimeOffset CreatedAt,
    BuilderState Builder,
    DateTimeOffset? BuilderRequestedAt,
    bool Suspended);

/// <summary>
/// What the admin door asks of Identity (ADM-11, AUTH-19, AUTH-20). It carries phone numbers,
/// so only the Admin service may use it (AUTH-02).
/// </summary>
public interface IUserAdmin
{
    /// <summary>Exact id, phone (any Iranian form), email (any case) or username, or the start of the name.</summary>
    Task<IReadOnlyList<AdminUser>> SearchAsync(string query, int limit, CancellationToken cancellationToken);

    /// <summary>Open builder requests, oldest first.</summary>
    Task<IReadOnlyList<AdminUser>> BuilderRequestsAsync(int limit, CancellationToken cancellationToken);

    /// <summary>Approves (Approved) or refuses (None) the user. Null when the user does not exist.</summary>
    Task<AdminUser?> SetBuilderAsync(long userId, bool approve, CancellationToken cancellationToken);

    /// <summary>AUTH-20: a suspended user is logged out everywhere at once. Null when the user does not exist.</summary>
    Task<AdminUser?> SetSuspendedAsync(long userId, bool suspended, CancellationToken cancellationToken);

    /// <summary>ADM-17: one user by id, or null.</summary>
    Task<AdminUser?> FindAsync(long userId, CancellationToken cancellationToken);

    /// <summary>An Iranian mobile number in any form as +989121234567, or null.</summary>
    string? NormalizePhone(string? input);

    /// <summary>An email address in lower case, or null.</summary>
    string? NormalizeEmail(string? input);
}
