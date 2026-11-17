using Dalan.Identity.Contracts;

namespace Dalan.Identity.Users;

public sealed class User
{
    public long Id { get; private set; }
    public required string Phone { get; set; }
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
