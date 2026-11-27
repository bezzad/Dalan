namespace Dalan.Sites;

/// <summary>
/// Every Sites limit, section "Sites" of the settings. Starting values are in appsettings.json
/// (openspec change l2-site-tunnel, design.md); nothing here is a default.
/// </summary>
public sealed class SitesOptions
{
    /// <summary>
    /// The registered domain of the sites, for example "dalancloud.ir". Empty: sites are off.
    /// It may carry a port for local work ("localhost:5000"); the host match ignores it (SITE-08).
    /// </summary>
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

    /// <summary>SITE-12: requests of one address that may wait in line after <see cref="MaxPendingPerAddress"/>.</summary>
    public int MaxQueuedPerAddress { get; set; }

    /// <summary>SITE-11: the most one streamed answer may carry in total.</summary>
    public int MaxAnswerMb { get; set; }

    /// <summary>SITE-22: the size of one part of a streamed answer, and how many parts may be on the way.</summary>
    public int StreamChunkKb { get; set; }

    public int StreamWindow { get; set; }
    public int SessionIdleSeconds { get; set; }
    public int PollTimeoutSeconds { get; set; }
    public int AccessCheckSeconds { get; set; }

    /// <summary>OPS-03: after the server's close frame, how long it waits for the agent to hang up before it cuts the connection.</summary>
    public int CloseWaitSeconds { get; set; }

    /// <summary>AGT-10: the folder of the agent files, next to the server file unless rooted. The deploy fills it.</summary>
    public string AgentFolder { get; set; } = "";

    /// <summary>SITE-18: the host of demo links, for example "dalans.ir". Empty: demos are off.</summary>
    public string DemoHost { get; set; } = "";

    public int DemoHours { get; set; }
    public int DemoIdLength { get; set; }
    public int MaxDemosPerBuilder { get; set; }

    public long MaxBodyBytes => MaxBodyMb * 1024L * 1024L;

    public long MaxAnswerBytes => MaxAnswerMb * 1024L * 1024L;

    /// <summary>A host setting without its port: "localhost:5000" gives "localhost".</summary>
    public static string HostOnly(string value)
    {
        var colon = value.LastIndexOf(':');
        return colon < 0 ? value : value[..colon];
    }

    /// <summary>The public address of a site.</summary>
    public string UrlOf(string name) => $"{Scheme}://{name}.{Domain}";

    /// <summary>The public address of a demo.</summary>
    public string DemoUrlOf(string id) => $"{Scheme}://{DemoHost}/{id}/";
}
