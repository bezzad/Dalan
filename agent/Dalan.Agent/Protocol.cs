using System.Text.Json.Serialization;

namespace Dalan.Agent;

// The tunnel protocol of the server (openspec change l2-site-tunnel, design.md). The agent shares no code with the server;
// the end-to-end tests in tests/Dalan.Sites.Tests run this agent against the real server, so both sides stay the same.

public sealed record OpenSession(string[] Sites, string? Demo = null);

public sealed record SiteAddress(string Name, string Url);

public sealed record DemoAddress(string Id, string Url, DateTimeOffset ExpiresAt);

public sealed record SessionOpened(string Session, SiteAddress[] Sites, int MaxBodyMb, DemoAddress? Demo = null);

public sealed record TunnelRequest(long Id, string Site, string Method, string Path, Dictionary<string, string[]> Headers, string? Body);

public sealed record TunnelResponse(long Id, int Status, Dictionary<string, string[]> Headers, string? Body);

public sealed record PollAnswer(TunnelRequest[] Requests);

public sealed record ServerError(string Error, string Message);

/// <summary>One site of the agent: its name and the local port of the app.</summary>
public sealed record SiteEntry(string Name, int Port);

/// <summary>AGT-02: the config file.</summary>
public sealed record AgentConfig(string? Server, string? Key, List<SiteEntry> Sites)
{
    public static AgentConfig Empty => new(null, null, []);
}

/// <summary>AGT-01: JSON by source generation, so Native AOT needs no reflection.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, WriteIndented = false)]
[JsonSerializable(typeof(OpenSession))]
[JsonSerializable(typeof(SessionOpened))]
[JsonSerializable(typeof(TunnelRequest))]
[JsonSerializable(typeof(TunnelResponse))]
[JsonSerializable(typeof(PollAnswer))]
[JsonSerializable(typeof(ServerError))]
[JsonSerializable(typeof(AgentConfig))]
public sealed partial class AgentJson : JsonSerializerContext;
