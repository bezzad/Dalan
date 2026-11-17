using Dalan.Relay.BotApi;
using Dalan.Relay.Bots;
using Dalan.Relay.Builder;
using Dalan.Relay.Buttons;
using Dalan.Relay.Chats;
using Dalan.Relay.ClientApi;
using Dalan.Relay.Contracts;
using Dalan.Relay.Data;
using Dalan.Relay.Files;
using Dalan.Relay.Live;
using Dalan.Relay.Outbox;
using Dalan.Relay.Queue;
using Dalan.Relay.Showcase;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dalan.Relay;

/// <summary>Entry point of the Relay service. The host calls these methods.</summary>
public static class RelayService
{
    public static IServiceCollection AddRelayService(this IServiceCollection services)
    {
        services.AddOptions<RelayOptions>().BindConfiguration("Relay");
        services.TryAddSingleton(TimeProvider.System);
        services.AddPooledDbContextFactory<RelayDb>((provider, options) => options
            .UseNpgsql(
                provider.GetRequiredService<IConfiguration>().GetConnectionString("Dalan"),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", RelayDb.Schema))
            .UseSnakeCaseNamingConvention());

        services.AddSingleton<IUpdateQueue, InMemoryUpdateQueue>();
        services.AddSingleton<BotRegistry>();
        services.AddSingleton<SuspendedBots>();
        services.AddSingleton<IBotAdmin, BotAdmin>();
        services.AddSingleton<ChatService>();
        services.AddSingleton<DeliveryMarks>();
        services.AddSingleton<BotPresence>();
        services.AddSingleton<ButtonPresses>();
        services.AddSingleton<FileStore>();
        services.AddSingleton<FileSweeper>();
        services.AddHostedService(provider => provider.GetRequiredService<FileSweeper>());
        services.AddSingleton<QueueLoader>();
        services.AddSingleton<RateLimits>();
        services.AddSingleton<IBotGateway, BotGateway>();
        services.AddSingleton<IMessageNotifier, InMemoryMessageNotifier>();
        services.AddSingleton<OutboxWorker>();
        services.AddHostedService(provider => provider.GetRequiredService<OutboxWorker>());
        services.AddSingleton<HistorySweeper>();
        services.AddHostedService(provider => provider.GetRequiredService<HistorySweeper>());
        services.AddSingleton<ExpirySweeper>();
        services.AddHostedService(provider => provider.GetRequiredService<ExpirySweeper>());
        return services;
    }

    public static IEndpointRouteBuilder MapRelayService(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapBotApi();
        endpoints.MapClientApi();
        endpoints.MapPublicShowcase();
        return endpoints;
    }

    /// <summary>OPS-07, Q-07 and LIVE-04: applies this service's migrations, reloads the queues, wakes the outbox worker. The host calls it before the server is ready.</summary>
    public static async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var dbFactory = services.GetRequiredService<IDbContextFactory<RelayDb>>();
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
            await db.Database.MigrateAsync(cancellationToken);

        await services.GetRequiredService<SuspendedBots>().LoadAsync(cancellationToken);
        await services.GetRequiredService<QueueLoader>().LoadAsync(cancellationToken);

        // LIVE-04: events that an earlier run saved but did not pass on.
        services.GetRequiredService<OutboxWorker>().Wake();
    }
}
