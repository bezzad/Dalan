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
}
