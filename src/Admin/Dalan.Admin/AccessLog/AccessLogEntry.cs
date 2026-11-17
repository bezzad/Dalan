using System.Net;
using Dalan.Admin.Contracts;

namespace Dalan.Admin.AccessLog;

/// <summary>ADM-01: one caller seen at one address at one moment.</summary>
public sealed class AccessLogEntry
{
    public long Id { get; set; }
    public AccessActor Actor { get; set; }
    public long ActorId { get; set; }
    public required IPAddress Address { get; set; }

    /// <summary>The client's source port; null when the proxy did not report it. Needed where many people share one address (CGNAT).</summary>
    public int? Port { get; set; }

    /// <summary>UTC.</summary>
    public DateTimeOffset At { get; set; }
}
