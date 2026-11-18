using System.Net;
using Dalan.Admin.Contracts;

namespace Dalan.Admin.AccessLog;

/// <summary>ADM-01, ADM-04: one caller seen at one address at one moment.</summary>
public sealed class AccessLogEntry
{
    public long Id { get; set; }
    public AccessActor Actor { get; set; }

    /// <summary>The user or bot id; 0 for a phone number or an email address.</summary>
    public long ActorId { get; set; }

    /// <summary>Only for <see cref="AccessActor.Phone"/>: the number the login code was sent to.</summary>
    public string? Phone { get; set; }

    /// <summary>Only for <see cref="AccessActor.Email"/>: the address the login code was sent to.</summary>
    public string? Email { get; set; }

    public required IPAddress Address { get; set; }

    /// <summary>The client's source port; null when the proxy did not report it. Needed where many people share one address (CGNAT).</summary>
    public int? Port { get; set; }

    /// <summary>UTC.</summary>
    public DateTimeOffset At { get; set; }
}
