using Dalan.Identity.Contracts;
using Dalan.Sites.Names;
using Dalan.Sites.Tunnel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Dalan.Sites.MySites;

internal sealed record SiteJson(long Id, string Name, string Url, bool Online, DateTimeOffset CreatedAt);

/// <summary>The sites, and what the guide in "My apps" needs: the sites domain (empty: sites are off) and the agent's server.</summary>
internal sealed record MySitesJson(string Domain, string Scheme, string TunnelUrl, IEnumerable<SiteJson> Sites);

/// <summary>SITE-16: a builder's own sites, for "My apps".</summary>
internal static class MySitesEndpoints
{
    public static void MapMySites(this IEndpointRouteBuilder endpoints)
    {
        var sites = endpoints.MapGroup("/api/sites").RequireAuthorization(AccessKeys.SessionOrKeyPolicy)
            .AddEndpointFilter<BuilderOnly>();

        sites.MapGet("", async (HttpContext http, SiteBook book, TunnelHub hub, IOptionsMonitor<SitesOptions> options) =>
        {
            var settings = options.CurrentValue;
            var mine = await book.ListAsync(TunnelEndpoints.UserId(http), http.RequestAborted);
            return SitesJson.Ok(new MySitesJson(settings.Domain, settings.Scheme, settings.TunnelUrl,
                mine.Select(s => new SiteJson(s.Id, s.Name, settings.UrlOf(s.Name), hub.IsOnline(s.Name), s.CreatedAt))));
        });

        sites.MapDelete("/{siteId:long}", async (long siteId, HttpContext http, SiteBook book, TunnelHub hub, IUserDirectory users) =>
        {
            var userId = TunnelEndpoints.UserId(http);
            var access = await users.GetAccessAsync(userId, http.RequestAborted);
            if (await book.DeleteAsync(userId, access.IsAdmin, siteId, http.RequestAborted) is not { } name)
                return SitesJson.Error(StatusCodes.Status404NotFound, "site_not_found", "You have no site with this id.");
            hub.Drop(name);
            return Results.NoContent();
        });
    }
}
