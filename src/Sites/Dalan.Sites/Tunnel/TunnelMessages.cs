namespace Dalan.Sites.Tunnel;

/// <summary>A visitor request on its way to the agent. The body is base64, null when empty.</summary>
public sealed record TunnelRequest(
    long Id, string Site, string Method, string Path, Dictionary<string, string[]> Headers, string? Body);

/// <summary>
/// The agent's answer to one request, or one part of it (SITE-22). The first message has the status and headers;
/// <see cref="More"/> says more parts follow. A part after the first has status 0.
/// </summary>
public sealed record TunnelResponse(long Id, int Status, Dictionary<string, string[]>? Headers, string? Body, bool More = false);

/// <summary>SITE-22: from the server to the agent over WebSocket: "ack" (one part was written) or "cancel" (stop this answer).</summary>
public sealed record TunnelControl(long Id, bool? Ack = null, bool? Cancel = null);

/// <summary>
/// Sites to serve, and optionally a demo: "new" or the id of a demo of the caller (SITE-18).
/// <see cref="Login"/> true (SITE-26, "dalan http --login") turns the proxy login on for these sites; null leaves it as it is.
/// </summary>
internal sealed record OpenSessionRequest(string[]? Sites, string? Demo = null, bool? Login = null);

internal sealed record SiteAddress(string Name, string Url);

internal sealed record DemoAddress(string Id, string Url, DateTimeOffset ExpiresAt);

internal sealed record OpenSessionAnswer(
    string Session, SiteAddress[] Sites, int MaxBodyMb, DemoAddress? Demo, int StreamChunkKb, int StreamWindow);

internal sealed record PollAnswer(TunnelRequest[] Requests);
