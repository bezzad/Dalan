using Dalan.Identity.Contracts;
using Dalan.Identity.Data;
using Dalan.Identity.Users;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Identity;

/// <summary>Entry point of the Identity service. The host calls these methods.</summary>
public static class IdentityService
{
    public static IServiceCollection AddIdentityService(this IServiceCollection services)
    {
        services.AddPooledDbContextFactory<IdentityDb>((provider, options) => options
            .UseNpgsql(
                provider.GetRequiredService<IConfiguration>().GetConnectionString("Dalan"),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", IdentityDb.Schema))
            .UseSnakeCaseNamingConvention());
        services.AddSingleton<IUserDirectory, UserDirectory>();
        return services;
    }

    public static IEndpointRouteBuilder MapIdentityService(this IEndpointRouteBuilder endpoints) => endpoints;

    /// <summary>OPS-07: applies this service's migrations. The host calls it before the server is ready.</summary>
    public static async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var dbFactory = services.GetRequiredService<IDbContextFactory<IdentityDb>>();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);
    }
}
