using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.HttpOverrides;
using IPNetwork = System.Net.IPNetwork;

namespace Dalan.Host;

/// <summary>
/// SEC-10: makes <c>HttpContext.Connection.RemoteIpAddress</c> and <c>RemotePort</c> the real client's.
/// <c>X-Forwarded-For</c> is believed only from the proxies in <c>Dalan:TrustedProxies</c>, because anyone can write that header.
/// Everything else (rate limit, access log) reads the connection and never the header.
/// </summary>
public static partial class ClientAddress
{
    private const string Header = "X-Forwarded-For";

    /// <summary>Addresses that are never a real client on the internet, so a header from them means "a proxy" (SEC-11).</summary>
    private static readonly IPNetwork[] PrivateNetworks =
    [
        .. new[] { "127.0.0.0/8", "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "100.64.0.0/10", "169.254.0.0/16", "::1/128", "fc00::/7", "fe80::/10" }
            .Select(IPNetwork.Parse),
    ];

    public static IServiceCollection AddClientAddress(this IServiceCollection services, IConfiguration configuration)
    {
        var trusted = TrustedProxies(configuration);
        return services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            // No hop count: walk the header from the right until the first address that is not a trusted proxy.
            options.ForwardLimit = null;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            foreach (var network in trusted) options.KnownIPNetworks.Add(network);
        });
    }

    public static IApplicationBuilder UseClientAddress(this WebApplication app)
    {
        var trusted = TrustedProxies(app.Configuration);
        var warned = new ConcurrentDictionary<IPAddress, bool>();
        var logger = app.Logger;

        // SEC-11: a proxy nobody listed would make every client look like the proxy. Say so, once per proxy.
        app.Use((http, next) =>
        {
            if (http.Connection.RemoteIpAddress is { } caller && http.Request.Headers.ContainsKey(Header))
            {
                if (caller.IsIPv4MappedToIPv6) caller = caller.MapToIPv4();
                if (Contains(PrivateNetworks, caller) && !Contains(trusted, caller) && warned.TryAdd(caller, true))
                    LogProxyNotTrusted(logger, caller);
            }

            return next(http);
        });

        return app.UseForwardedHeaders();
    }

    /// <summary>Reads <c>Dalan:TrustedProxies</c>: addresses or CIDR ranges, comma separated. A wrong entry stops the start.</summary>
    private static IPNetwork[] TrustedProxies(IConfiguration configuration) =>
    [
        .. (configuration["Dalan:TrustedProxies"] ?? "")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseProxy),
    ];

    private static IPNetwork ParseProxy(string entry)
    {
        if (entry.Contains('/', StringComparison.Ordinal)) return IPNetwork.Parse(entry);

        var address = IPAddress.Parse(entry);
        return new IPNetwork(address, address.AddressFamily == AddressFamily.InterNetwork ? 32 : 128);
    }

    private static bool Contains(IPNetwork[] networks, IPAddress address) => networks.Any(network => network.Contains(address));

    [LoggerMessage(LogLevel.Warning,
        "A call with X-Forwarded-For came from {Address}, which is not in Dalan:TrustedProxies, so the header was ignored. "
        + "If {Address} is our proxy or load balancer, add it to Dalan:TrustedProxies; until then every client behind it is recorded with its address.")]
    private static partial void LogProxyNotTrusted(ILogger logger, IPAddress address);
}
