namespace Dalan.Identity.Sessions;

/// <summary>One logged-in device (AUTH-08). Holds only hashes of its refresh token.</summary>
public sealed class Session
{
    public long Id { get; private set; }
    public long UserId { get; set; }

    /// <summary>SHA-256 of the refresh token that works now.</summary>
    public required byte[] TokenHash { get; set; }

    /// <summary>SHA-256 of the token before it. If that one comes back, it was copied (AUTH-10).</summary>
    public byte[]? PreviousHash { get; set; }

    /// <summary>How the session started: "sms", "pwd" or "dev".</summary>
    public required string Method { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset RotatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
