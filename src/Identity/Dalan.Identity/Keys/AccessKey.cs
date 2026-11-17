namespace Dalan.Identity.Keys;

/// <summary>AUTH-21: one access key. Holds only the SHA-256 of the key.</summary>
public sealed class AccessKey
{
    public long Id { get; private set; }
    public long UserId { get; set; }
    public required string Name { get; set; }
    public required byte[] KeyHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
