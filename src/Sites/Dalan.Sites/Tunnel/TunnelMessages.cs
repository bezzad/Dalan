namespace Dalan.Sites.Tunnel;

/// <summary>A visitor request on its way to the agent. The body is base64, null when empty.</summary>
public sealed record TunnelRequest(
    long Id, string Site, string Method, string Path, Dictionary<string, string[]> Headers, string? Body);

/// <summary>The agent's answer to one request.</summary>
public sealed record TunnelResponse(long Id, int Status, Dictionary<string, string[]>? Headers, string? Body);

internal sealed record OpenSessionRequest(string[]? Sites);

internal sealed record SiteAddress(string Name, string Url);

internal sealed record OpenSessionAnswer(string Session, SiteAddress[] Sites, int MaxBodyMb);

internal sealed record PollAnswer(TunnelRequest[] Requests);
