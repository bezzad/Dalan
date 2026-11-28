using Dalan.Notification.Contracts;
using Dalan.Notification.Data;
using Dalan.Notification.Email;
using Dalan.Notification.Inbox;
using Dalan.Notification.Push;
using Dalan.Notification.Sms;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dalan.Notification;

/// <summary>Entry point of the Notification service. The host calls these methods.</summary>
public static class NotificationService
{
    public static IServiceCollection AddNotificationService(this IServiceCollection services)
    {
        services.AddOptions<NotificationOptions>().BindConfiguration("Notification");
        services.AddSingleton<NoSmsSender>();
        services.AddSingleton<LogSmsSender>();
        // NTF-02: no request log for this client, so the API key header stays out of the log.
        services.AddHttpClient<SmsIrSmsSender>().RemoveAllLoggers();
        // A new one for each use, so the HTTP client under it is renewed by the factory.
        services.AddTransient(SmsSender);
        services.AddSingleton<NoEmailSender>();
        services.AddSingleton<LogEmailSender>();
        services.AddSingleton<SmtpEmailSender>();
        services.AddSingleton(EmailSender);

        services.TryAddSingleton(TimeProvider.System);
        services.AddPooledDbContextFactory<NotificationDb>((provider, options) => options
            .UseNpgsql(
                provider.GetRequiredService<IConfiguration>().GetConnectionString("Dalan"),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", NotificationDb.Schema))
            .UseSnakeCaseNamingConvention());
        services.AddSingleton<WebPushService>();
        services.AddSingleton<IWebPush>(provider => provider.GetRequiredService<WebPushService>());
        services.AddHostedService<PushWorker>();
        // NTF-09: no request log, because an endpoint address is a secret of its browser.
        services.AddHttpClient(nameof(PushWorker), (provider, client) => client.Timeout =
                TimeSpan.FromSeconds(provider.GetRequiredService<IOptions<NotificationOptions>>().Value.Push.TimeoutSeconds))
            .RemoveAllLoggers();
        services.AddSingleton<InboxService>();
        services.AddSingleton<IInbox>(provider => provider.GetRequiredService<InboxService>());
        services.AddSingleton<InboxSweep>();
        services.AddHostedService(provider => provider.GetRequiredService<InboxSweep>());
        return services;
    }

    public static IEndpointRouteBuilder MapNotificationService(this IEndpointRouteBuilder endpoints)
    {
        // NTF-01: a wrong provider setting stops the start, not the first login.
        endpoints.ServiceProvider.GetRequiredService<ISmsSender>();
        // NTF-10, NTF-11: and a wrong email setting.
        endpoints.ServiceProvider.GetRequiredService<IEmailSender>();
        // NTF-05: so do wrong push keys.
        endpoints.ServiceProvider.GetRequiredService<IWebPush>();
        endpoints.MapInbox();
        return endpoints;
    }

    /// <summary>OPS-07: applies this service's migrations. The host calls it before the server is ready.</summary>
    public static async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var dbFactory = services.GetRequiredService<IDbContextFactory<NotificationDb>>();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);
    }

    private static ISmsSender SmsSender(IServiceProvider services)
    {
        var provider = services.GetRequiredService<IOptions<NotificationOptions>>().Value.SmsProvider?.Trim() ?? "";
        switch (provider)
        {
            case "":
                return services.GetRequiredService<NoSmsSender>();
            case "smsir":
                return services.GetRequiredService<SmsIrSmsSender>();
            case "log" when services.GetRequiredService<IHostEnvironment>().IsDevelopment()
                         || !string.IsNullOrWhiteSpace(services.GetRequiredService<IOptions<NotificationOptions>>().Value.LogSmsPhones):
                return services.GetRequiredService<LogSmsSender>();
            case "log":
                throw new InvalidOperationException(
                    "Notification:SmsProvider is 'log', but this is not the Development environment and Notification:LogSmsPhones is empty. " +
                    "The log sender writes login codes to the log and sends no SMS. Set a real provider, or list the test numbers.");
            default:
                throw new InvalidOperationException(
                    $"Notification:SmsProvider is '{provider}'. Known providers: smsir, log, or empty.");
        }
    }

    private static IEmailSender EmailSender(IServiceProvider services)
    {
        var settings = services.GetRequiredService<IOptions<NotificationOptions>>().Value;
        var provider = settings.EmailProvider?.Trim() ?? "";
        switch (provider)
        {
            case "":
                return services.GetRequiredService<NoEmailSender>();
            case "smtp" when string.IsNullOrWhiteSpace(settings.Smtp.Host) || string.IsNullOrWhiteSpace(settings.Smtp.From):
                throw new InvalidOperationException(
                    "Notification:EmailProvider is 'smtp', but Notification:Smtp:Host or Notification:Smtp:From is empty.");
            case "smtp":
                return services.GetRequiredService<SmtpEmailSender>();
            case "log" when services.GetRequiredService<IHostEnvironment>().IsDevelopment()
                         || !string.IsNullOrWhiteSpace(settings.LogEmailAddresses):
                return services.GetRequiredService<LogEmailSender>();
            case "log":
                throw new InvalidOperationException(
                    "Notification:EmailProvider is 'log', but this is not the Development environment and Notification:LogEmailAddresses is empty. " +
                    "The log sender writes login codes to the log and sends no email. Set smtp, or list the test addresses.");
            default:
                throw new InvalidOperationException(
                    $"Notification:EmailProvider is '{provider}'. Known providers: smtp, log, or empty.");
        }
    }
}
