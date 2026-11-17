using System.Net;

namespace Dalan.Admin.Contracts;

/// <summary>Who can be the caller in the access log.</summary>
public enum AccessActor
{
    User = 1,
    Bot = 2,
}

/// <summary>
/// ADM-01: the record of who called from which address, kept to answer legal complaints.
/// Every door that knows its caller calls this before it serves the call.
/// </summary>
public interface IAccessLog
{
    /// <summary>
    /// Records the call. Cheap: it writes a row only for a new address or after the interval (ADM-02).
    /// Pass the address and port of the connection as they are (<c>HttpContext.Connection</c>); the host made them real (SEC-06).
    /// </summary>
    ValueTask RecordAsync(AccessActor actor, long actorId, IPAddress? address, int port, CancellationToken cancellationToken);
}
