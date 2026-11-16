using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Identity;

/// <summary>Entry point of the Identity service. The host calls these two methods.</summary>
public static class IdentityService
{
    public static IServiceCollection AddIdentityService(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapIdentityService(this IEndpointRouteBuilder endpoints) => endpoints;
}
