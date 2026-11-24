using System.Net;

namespace Dalan.Admin.Contact;

/// <summary>ADM-17: a question sent from the contact page.</summary>
public sealed class ContactMessage
{
    public long Id { get; set; }

    /// <summary>The sender when logged in; null for a visitor.</summary>
    public long? UserId { get; set; }

    /// <summary>Where the admin answers. A logged-in user's own email when they have one.</summary>
    public required string Email { get; set; }

    public required string Text { get; set; }

    /// <summary>The real address of the connection (SEC-10), kept for legal complaints (rule 13).</summary>
    public required IPAddress Address { get; set; }

    public int? Port { get; set; }

    /// <summary>UTC.</summary>
    public DateTimeOffset At { get; set; }

    public bool Read { get; set; }
}
