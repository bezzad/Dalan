using Dalan.Identity.Contracts;
using Dalan.Sites.Data;
using Dalan.Sites.Demos;
using Dalan.Sites.Downloads;
using Dalan.Sites.MySites;
using Dalan.Sites.Names;
using Dalan.Sites.Proxy;
using Dalan.Sites.Tunnel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Dalan.Sites;

/// <summary>Entry point of the Sites service. The host calls these methods.</summary>
public static class SitesService
{
    public static IServiceCollection AddSitesService(this IServiceCollection services)
    {
        services.AddOptions<SitesOptions>().BindConfiguration("Sites")
            .Validate<IConfiguration>((options, configuration) => !IsUnderDalan(options.Domain, configuration["Admin:SiteUrl"]),
                "Sites:Domain must be another registered domain than the host of Admin:SiteUrl (SITE-13): a site's script could set cookies for Dalan.")
            .Validate<IConfiguration>((options, configuration) => !IsUnderDalan(options.DemoHost, configuration["Admin:SiteUrl"]),
                "Sites:DemoHost must not be Dalan's own host or a name under it (SITE-18): the demo host takes every request of its host.")
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        services.AddPooledDbContextFactory<SitesDb>((provider, options) => options
            .UseNpgsql(
                provider.GetRequiredService<IConfiguration>().GetConnectionString("Dalan"),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", SitesDb.Schema))
            .UseSnakeCaseNamingConvention());
        services.AddSingleton<SiteBook>();
        services.AddSingleton<IUserHoldings, SiteHoldings>();
        services.AddSingleton<DemoBook>();
        services.AddSingleton<TunnelHub>();
        services.AddSingleton<SiteProxy>();
        return services;
    }

    /// <summary>
    /// SITE-08: a request to a site host goes to the site and nowhere else in Dalan. Put it before the host's own
    /// pages and headers; <paramref name="branch"/> adds what every site request still needs (the per-address limit).
    /// </summary>
    public static IApplicationBuilder UseSites(this IApplicationBuilder app, Action<IApplicationBuilder> branch)
    {
        var options = app.ApplicationServices.GetRequiredService<IOptionsMonitor<SitesOptions>>();
        return app.MapWhen(
            http => SiteProxy.IsDemoHost(http.Request.Host, options.CurrentValue)
                || SiteProxy.SiteOf(http.Request.Host, options.CurrentValue) is not null,
            site =>
            {
                branch(site);
                site.Run(http => SiteProxy.IsDemoHost(http.Request.Host, options.CurrentValue)
                    ? http.RequestServices.GetRequiredService<SiteProxy>().HandleDemoAsync(http)
                    : http.RequestServices.GetRequiredService<SiteProxy>()
                        .HandleAsync(http, SiteProxy.SiteOf(http.Request.Host, options.CurrentValue) ?? ""));
            });
    }

    public static IEndpointRouteBuilder MapSitesService(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapTunnel();
        endpoints.MapMySites();
        endpoints.MapCertificateCheck();
        endpoints.MapAgentDownloads();
        return endpoints;
    }

    /// <summary>OPS-07: applies this service's migrations. The host calls it before the server is ready.</summary>
    public static async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var db = await services.GetRequiredService<IDbContextFactory<SitesDb>>().CreateDbContextAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);
    }

    /// <summary>SITE-13: true when the sites domain is Dalan's own host or a name under it.</summary>
    internal static bool IsUnderDalan(string sitesDomain, string? siteUrl)
    {
        if (sitesDomain.Length == 0 || !Uri.TryCreate(siteUrl, UriKind.Absolute, out var url)) return false;
        var sites = SitesOptions.HostOnly(sitesDomain).ToLowerInvariant();
        var dalan = url.Host.ToLowerInvariant();
        return sites == dalan || sites.EndsWith("." + dalan, StringComparison.Ordinal);
    }
}
