namespace Dalan.Sites;

/// <summary>
/// Every Sites limit, section "Sites" of the settings. Starting values are in appsettings.json
/// (openspec change l2-site-tunnel, design.md); nothing here is a default.
/// </summary>
public sealed class SitesOptions
{
    /// <summary>The registered domain of the sites, for example "dalancloud.ir". Empty: sites are off.</summary>
    public string Domain { get; set; } = "";

    /// <summary>The scheme of the printed site address: "https", or "http" in local work.</summary>
    public string Scheme { get; set; } = "";

    /// <summary>The address the agent connects to: a hostname, never an IP address (portability).</summary>
    public string TunnelUrl { get; set; } = "";

    public int MaxSitesPerBuilder { get; set; }
    public int MaxNameLength { get; set; }

    /// <summary>Comma separated names nobody may take.</summary>
    public string ReservedNames { get; set; } = "";

    public int RequestTimeoutSeconds { get; set; }
    public int MaxBodyMb { get; set; }
    public int MaxPendingPerAddress { get; set; }
    public int SessionIdleSeconds { get; set; }
    public int PollTimeoutSeconds { get; set; }
    public int AccessCheckSeconds { get; set; }

    /// <summary>AGT-10: the folder of the agent files, next to the server file unless rooted. The deploy fills it.</summary>
    public string AgentFolder { get; set; } = "";

    public long MaxBodyBytes => MaxBodyMb * 1024L * 1024L;

    /// <summary>The public address of a site.</summary>
    public string UrlOf(string name) => $"{Scheme}://{name}.{Domain}";
}
