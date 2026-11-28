using Dalan.Admin.Contracts;
using Dalan.Identity.Contracts;
using Dalan.Sites.Login;
using Dalan.Sites.Names;
using Dalan.Sites.Tunnel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Dalan.Sites.MySites;

internal sealed record ReserveRequest(string? Name);

internal sealed record LoginRequest(bool? On);

/// <summary>SITE-16; <see cref="Login"/>: SITE-26, the proxy asks for Dalan's login.</summary>
internal sealed record SiteJson(long Id, string Name, string Url, bool Online, DateTimeOffset CreatedAt, bool Login);

/// <summary>The sites, and what the guide in "My apps" needs: the sites domain (empty: sites are off) and the agent's server.</summary>
internal sealed record MySitesJson(string Domain, string Scheme, string TunnelUrl, IEnumerable<SiteJson> Sites);

/// <summary>SITE-16: a builder's own sites, for "My apps".</summary>
internal static class MySitesEndpoints
{
    public static void MapMySites(this IEndpointRouteBuilder endpoints)
    {
        var sites = endpoints.MapGroup("/api/sites").RequireAuthorization(AccessKeys.SessionOrKeyPolicy)
            .AddEndpointFilter<BuilderOnly>();

        sites.MapGet("", async (HttpContext http, SiteBook book, TunnelHub hub, SiteLogins logins, IOptionsMonitor<SitesOptions> options) =>
        {
            var settings = options.CurrentValue;
            var mine = await book.ListAsync(TunnelEndpoints.UserId(http), http.RequestAborted);
            return SitesJson.Ok(new MySitesJson(settings.Domain, settings.Scheme, settings.TunnelUrl,
                mine.Select(s => new SiteJson(s.Id, s.Name, settings.UrlOf(s.Name), hub.IsOnline(s.Name), s.CreatedAt, logins.IsOn(s.Name)))));
        });

        // SITE-26: the switch "Require login on the proxy".
        sites.MapPut("/{siteId:long}/login", async (
            long siteId, HttpContext http, SiteBook book, TunnelHub hub, SiteLogins logins, IAuditLog audit, IOptionsMonitor<SitesOptions> options) =>
        {
            var userId = TunnelEndpoints.UserId(http);
            if (await SitesJson.ReadAsync<LoginRequest>(http) is not { On: { } on })
                return SitesJson.Error(StatusCodes.Status400BadRequest, "bad_request", "Send {\"on\":true} or {\"on\":false}.");
            if (!await logins.SetAsync(userId, siteId, on, http.RequestAborted))
                return SitesJson.Error(StatusCodes.Status404NotFound, "site_not_found", "You have no site with this id.");
            var site = (await book.ListAsync(userId, http.RequestAborted)).Single(s => s.Id == siteId);
            await audit.RecordAsync(userId, on ? "site.login.on" : "site.login.off", $"site {site.Name}", null, http.RequestAborted);
            var settings = options.CurrentValue;
            return SitesJson.Ok(new SiteJson(site.Id, site.Name, settings.UrlOf(site.Name), hub.IsOnline(site.Name), site.CreatedAt, on));
        });

        // SITE-24: a name is kept before any agent runs; 200 when it is already the caller's.
        sites.MapPost("", async (HttpContext http, SiteBook book, TunnelHub hub, SiteLogins logins, IOptionsMonitor<SitesOptions> options) =>
        {
            var settings = options.CurrentValue;
            if (settings.Domain.Length == 0)
                return SitesJson.Error(StatusCodes.Status503ServiceUnavailable, "sites_off", "Sites are not turned on in this server yet.");
            var name = (await SitesJson.ReadAsync<ReserveRequest>(http))?.Name;
            var userId = TunnelEndpoints.UserId(http);
            var before = await book.ListAsync(userId, http.RequestAborted);
            var (error, bad, claimed) = await book.ClaimAsync(userId, [name], http.RequestAborted);
            if (error != ClaimError.None) return TunnelEndpoints.ClaimFailed(error, bad, settings);
            var site = (await book.ListAsync(userId, http.RequestAborted)).Single(s => s.Name == claimed[0]);
            var json = new SiteJson(site.Id, site.Name, settings.UrlOf(site.Name), hub.IsOnline(site.Name), site.CreatedAt, logins.IsOn(site.Name));
            return before.Any(s => s.Id == site.Id) ? SitesJson.Ok(json) : Results.Json(json, SitesJson.Options, statusCode: StatusCodes.Status201Created);
        });

        sites.MapDelete("/{siteId:long}", async (long siteId, HttpContext http, SiteBook book, TunnelHub hub, SiteLogins logins, IUserDirectory users) =>
        {
            var userId = TunnelEndpoints.UserId(http);
            var access = await users.GetAccessAsync(userId, http.RequestAborted);
            // SEC-15: an admin deletes other users' sites only with the second factor on.
            if (await book.DeleteAsync(userId, access.ActsAsAdmin, siteId, http.RequestAborted) is not { } name)
                return SitesJson.Error(StatusCodes.Status404NotFound, "site_not_found", "You have no site with this id.");
            hub.Drop(name);
            logins.Forget(name);
            return Results.NoContent();
        });
    }
}
