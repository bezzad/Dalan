using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Relay;

/// <summary>Entry point of the Relay service. The host calls these two methods.</summary>
public static class RelayService
{
    public static IServiceCollection AddRelayService(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapRelayService(this IEndpointRouteBuilder endpoints) => endpoints;
}
