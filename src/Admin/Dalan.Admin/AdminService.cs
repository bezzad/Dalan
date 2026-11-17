using Dalan.Admin.AccessLog;
using Dalan.Admin.AdminApi;
using Dalan.Admin.Audit;
using Dalan.Admin.Settings;
using Dalan.Admin.Contracts;
using Dalan.Admin.Data;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dalan.Admin;

/// <summary>Entry point of the Admin service. The host calls these methods.</summary>
public static class AdminService
{
    public static IServiceCollection AddAdminService(this IServiceCollection services)
    {
        services.AddOptions<AdminOptions>().BindConfiguration("Admin");
        services.TryAddSingleton(TimeProvider.System);
        services.AddPooledDbContextFactory<AdminDb>((provider, options) => options
            .UseNpgsql(
                provider.GetRequiredService<IConfiguration>().GetConnectionString("Dalan"),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", AdminDb.Schema))
            .UseSnakeCaseNamingConvention());

        services.AddSingleton<AccessLogService>();
        services.AddSingleton<IAccessLog>(provider => provider.GetRequiredService<AccessLogService>());
        services.AddHostedService<AccessLogSweeper>();
        services.AddSingleton<IAuditLog, AuditLogService>();
        services.AddSingleton<SettingsCatalog>();
        services.AddSingleton<SettingsService>();
        return services;
    }

    public static IEndpointRouteBuilder MapAdminService(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAdminApi();
        return endpoints;
    }

    /// <summary>OPS-07: applies this service's migrations. The host calls it before the server is ready.</summary>
    public static async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var dbFactory = services.GetRequiredService<IDbContextFactory<AdminDb>>();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);

        // ADM-07: the admin's values hold before the other services start and before the first call.
        await services.GetRequiredService<SettingsService>().LoadAsync(cancellationToken);
    }
}
