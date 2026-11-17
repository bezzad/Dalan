namespace Dalan.Notification.Push;

/// <summary>NTF-03: one browser of one user that takes pushes.</summary>
public sealed class Subscription
{
    public long Id { get; private set; }
    public long UserId { get; set; }
    public required string Endpoint { get; set; }
    public required string P256dh { get; set; }
    public required string Auth { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
