using Dalan.Identity.Contracts;
using Dalan.Relay.Builder;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Relay.Mcp;

/// <summary>
/// REL-46 and REL-47: the MCP server for builders, at /api/mcp, with the official SDK. Stateless: no memory per agent,
/// so it fits more than one server. Inside the Client API group, like the builder REST calls.
/// </summary>
internal static class McpDoor
{
    public static IServiceCollection AddMcpDoor(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddMcpServer(server => server.ServerInfo = new() { Name = "dalan", Version = "1" })
            .WithHttpTransport(http => http.Stateless = true)
            .WithTools([typeof(BuilderTools)]);
        return services;
    }

    public static void MapMcpDoor(this RouteGroupBuilder api) =>
        api.MapGroup("/mcp").RequireAuthorization(AccessKeys.SessionOrKeyPolicy).AddEndpointFilter<BuilderFilter>().MapMcp();
}
