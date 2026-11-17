using Dalan.Notification.Contracts;
using Dalan.Notification.Sms;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dalan.Notification;

/// <summary>Entry point of the Notification service. The host calls these two methods.</summary>
public static class NotificationService
{
    public static IServiceCollection AddNotificationService(this IServiceCollection services)
    {
        services.AddOptions<NotificationOptions>().BindConfiguration("Notification");
        services.AddSingleton<NoSmsSender>();
        services.AddSingleton<LogSmsSender>();
        // NTF-02: no request log for this client, because the API key is part of the address.
        services.AddHttpClient<KavenegarSmsSender>().RemoveAllLoggers();
        // A new one for each use, so the HTTP client under it is renewed by the factory.
        services.AddTransient(SmsSender);
        return services;
    }

    public static IEndpointRouteBuilder MapNotificationService(this IEndpointRouteBuilder endpoints)
    {
        // NTF-01: a wrong provider setting stops the start, not the first login.
        endpoints.ServiceProvider.GetRequiredService<ISmsSender>();
        return endpoints;
    }

    private static ISmsSender SmsSender(IServiceProvider services)
    {
        var provider = services.GetRequiredService<IOptions<NotificationOptions>>().Value.SmsProvider?.Trim() ?? "";
        switch (provider)
        {
            case "":
                return services.GetRequiredService<NoSmsSender>();
            case "kavenegar":
                return services.GetRequiredService<KavenegarSmsSender>();
            case "log" when services.GetRequiredService<IHostEnvironment>().IsDevelopment():
                return services.GetRequiredService<LogSmsSender>();
            case "log":
                throw new InvalidOperationException(
                    "Notification:SmsProvider is 'log', but this is not the Development environment. " +
                    "The log sender writes login codes to the log and sends no SMS. Set a real provider.");
            default:
                throw new InvalidOperationException(
                    $"Notification:SmsProvider is '{provider}'. Known providers: kavenegar, log (Development only), or empty.");
        }
    }
}
