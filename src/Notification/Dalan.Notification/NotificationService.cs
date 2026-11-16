using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Notification;

/// <summary>Entry point of the Notification service. The host calls these two methods.</summary>
public static class NotificationService
{
    public static IServiceCollection AddNotificationService(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapNotificationService(this IEndpointRouteBuilder endpoints) => endpoints;
}
