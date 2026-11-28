using System.Text.Json.Serialization;

namespace Dalan.Agent;

// The tunnel protocol of the server (openspec change l2-site-tunnel, design.md). The agent shares no code with the server;
// the end-to-end tests in tests/Dalan.Sites.Tests run this agent against the real server, so both sides stay the same.

/// <summary><see cref="Login"/> true (SITE-26, "--login") asks the server to require Dalan's login on these sites; null leaves it.</summary>
public sealed record OpenSession(string[] Sites, string? Demo = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Login = null);

public sealed record SiteAddress(string Name, string Url);

public sealed record DemoAddress(string Id, string Url, DateTimeOffset ExpiresAt);

/// <summary>SITE-22: with <see cref="StreamChunkKb"/> 0 (an older server) the agent sends every answer whole.</summary>
public sealed record SessionOpened(
    string Session, SiteAddress[] Sites, int MaxBodyMb, DemoAddress? Demo = null, int StreamChunkKb = 0, int StreamWindow = 0);

/// <summary>A visitor request; or, over WebSocket, "ack" or "cancel" for a streamed answer (AGT-12), with only the id.</summary>
public sealed record TunnelRequest(
    long Id, string Site, string Method, string Path, Dictionary<string, string[]> Headers, string? Body, bool? Ack = null, bool? Cancel = null);

/// <summary>An answer, or one part of it (AGT-12): <see cref="More"/> says more parts follow; a later part has status 0.</summary>
public sealed record TunnelResponse(long Id, int Status, Dictionary<string, string[]>? Headers, string? Body, bool More = false);

/// <summary>AGT-15: the builder's sites, as "My apps" lists them.</summary>
public sealed record MySites(MySite[] Sites);

public sealed record MySite(string Name, string Url, bool Online);

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
[JsonSerializable(typeof(MySites))]
[JsonSerializable(typeof(AgentConfig))]
public sealed partial class AgentJson : JsonSerializerContext;
