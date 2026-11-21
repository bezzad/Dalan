namespace Dalan.Sites.Demos;

/// <summary>SITE-18: a demo link of a builder. The row stays after the end, so its owner can be named later.</summary>
public sealed class Demo
{
    public required string Id { get; set; }
    public long OwnerUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>SITE-20: a demo ends when its owner ends it or its time is over.</summary>
    public bool IsLive(DateTimeOffset now) => EndedAt is null && ExpiresAt > now;
}
