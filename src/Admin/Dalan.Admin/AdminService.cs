using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Admin;

/// <summary>Entry point of the Admin service. The host calls these two methods.</summary>
public static class AdminService
{
    public static IServiceCollection AddAdminService(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapAdminService(this IEndpointRouteBuilder endpoints) => endpoints;
}
