namespace Dalan.Identity.Users;

public sealed class User
{
    public long Id { get; private set; }
    public required string Phone { get; set; }
    public required string DisplayName { get; set; }
    public string? Username { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
