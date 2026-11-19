using Dalan.Identity.Contracts;

namespace Dalan.Identity.Users;

public sealed class User
{
    public long Id { get; private set; }
    /// <summary>Null for a user who logged in only by email (level 1). A user with a phone is level 2 (AUTH-19, AUTH-26).</summary>
    public string? Phone { get; set; }

    /// <summary>In lower case. Null for a user who logged in only by SMS (AUTH-24).</summary>
    public string? Email { get; set; }

    /// <summary>The id of the user's GitHub account, set by the first GitHub login (AUTH-28).</summary>
    public long? GithubId { get; set; }

    public required string DisplayName { get; set; }
    public string? Username { get; set; }

    /// <summary>Argon2id in PHC text form, or null when the user never set a password (AUTH-13).</summary>
    public string? PasswordHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>AUTH-19.</summary>
    public BuilderState Builder { get; set; }

    public DateTimeOffset? BuilderRequestedAt { get; set; }

    /// <summary>AUTH-20: set while an admin has suspended the user.</summary>
    public DateTimeOffset? SuspendedAt { get; set; }
}
