namespace Dalan.Notification.Inbox;

/// <summary>NTF-12: one notice in a user's inbox.</summary>
public sealed class Notice
{
    public long Id { get; private set; }
    public long UserId { get; set; }
    public required string Title { get; set; }
    public required string Text { get; set; }

    /// <summary>A path on this site, like /dashboard/admin/users/7, or null.</summary>
    public string? Link { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public bool Read { get; set; }

    /// <summary>NTF-13: when an SMS of this notice was sent; null when none was.</summary>
    public DateTimeOffset? SmsAt { get; set; }
}
