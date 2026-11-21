namespace Dalan.Sites.Tunnel;

/// <summary>A visitor request on its way to the agent. The body is base64, null when empty.</summary>
public sealed record TunnelRequest(
    long Id, string Site, string Method, string Path, Dictionary<string, string[]> Headers, string? Body);

/// <summary>The agent's answer to one request.</summary>
public sealed record TunnelResponse(long Id, int Status, Dictionary<string, string[]>? Headers, string? Body);

/// <summary>Sites to serve, and optionally a demo: "new" or the id of a demo of the caller (SITE-18).</summary>
internal sealed record OpenSessionRequest(string[]? Sites, string? Demo = null);

internal sealed record SiteAddress(string Name, string Url);

internal sealed record DemoAddress(string Id, string Url, DateTimeOffset ExpiresAt);

internal sealed record OpenSessionAnswer(string Session, SiteAddress[] Sites, int MaxBodyMb, DemoAddress? Demo = null);

internal sealed record PollAnswer(TunnelRequest[] Requests);
