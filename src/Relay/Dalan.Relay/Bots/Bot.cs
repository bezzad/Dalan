namespace Dalan.Relay.Bots;

public sealed class Bot
{
    public long Id { get; private set; }
    public required string Username { get; set; }
    public required string Name { get; set; }
    public long OwnerUserId { get; set; }

    /// <summary>SHA-256 of the token secret. The token itself is never stored (REL-03).</summary>
    public required byte[] TokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
