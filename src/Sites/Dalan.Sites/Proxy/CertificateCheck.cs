using Dalan.Relay.Contracts;
using Dalan.Sites.Names;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Dalan.Sites.Proxy;

/// <summary>
/// SITE-17: the front proxy asks this before it gets a certificate for a host on its first visit (OPS-15).
/// Yes only for a claimed site name, the demo host when demos are on, or the file view host when the viewer is on (SEC-16);
/// only the server itself may ask.
/// </summary>
internal static class CertificateCheck
{
    public static void MapCertificateCheck(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/api/sites/tls", async (string? domain, HttpContext http, SiteBook book, IOptionsMonitor<SitesOptions> options, IFileViewHost view) =>
        {
            if (http.Connection.RemoteIpAddress is not { } caller || !System.Net.IPAddress.IsLoopback(caller)) return Results.NotFound();
            if (!string.IsNullOrEmpty(domain) && view.Host is { } viewHost
                && string.Equals(domain, SitesOptions.HostOnly(viewHost), StringComparison.OrdinalIgnoreCase))
                return Results.Ok();
            if (!string.IsNullOrEmpty(domain) && SiteProxy.IsDemoHost(new HostString(domain), options.CurrentValue)) return Results.Ok();
            var site = string.IsNullOrEmpty(domain) ? null : SiteProxy.SiteOf(new HostString(domain), options.CurrentValue);
            return !string.IsNullOrEmpty(site) && await book.ExistsAsync(site, http.RequestAborted) ? Results.Ok() : Results.NotFound();
        }).DisableRateLimiting();
}
