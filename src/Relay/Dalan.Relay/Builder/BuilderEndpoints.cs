using System.Globalization;
using Dalan.Identity.Contracts;
using Dalan.Relay.ClientApi;
using Dalan.Relay.Data;
using Dalan.Relay.Showcase;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Dalan.Relay.Builder;

/// <summary>
/// REL-36, REL-37, REL-39, REL-45: a builder's own bots over REST. In the Client API group, so the access log (ADM-01)
/// and the per-user limit (SEC-03) hold. A session or an access key (AUTH-22) may call.
/// </summary>
internal static class BuilderEndpoints
{
    public static void MapBuilder(this RouteGroupBuilder api)
    {
        var builder = api.MapGroup("/builder").RequireAuthorization(AccessKeys.SessionOrKeyPolicy).AddEndpointFilter<BuilderFilter>();
        builder.MapGet("/bots", (HttpContext http, BuilderBots bots) =>
            Answer(bots.ListAsync(ClientApiFilter.UserId(http), http.RequestAborted)));
        builder.MapGet("/bots/{botId:long}", (long botId, HttpContext http, BuilderBots bots) =>
            Answer(bots.GetAsync(ClientApiFilter.UserId(http), botId, http.RequestAborted)));
        builder.MapPost("/bots", async (HttpContext http, BuilderBots bots) =>
            await Answer(bots.CreateAsync(ClientApiFilter.UserId(http), await ApiJson.ReadAsync<CreateBotRequest>(http), http.RequestAborted)));
        builder.MapPost("/bots/{botId:long}/token", (long botId, HttpContext http, BuilderBots bots) =>
            Answer(bots.RotateAsync(ClientApiFilter.UserId(http), botId, http.RequestAborted)));
        builder.MapShowcaseForBuilder();
    }

    private static async Task<IResult> Answer(Task<BuilderAnswer> call)
    {
        var answer = await call;
        return Results.Json(answer.Body, ApiJson.Options, statusCode: answer.Status);
    }

    /// <summary>REL-37 and REL-40: only the owner of the bot, or an admin.</summary>
    internal static Task<bool> MayChangeAsync(RelayDb db, BuilderBots bots, HttpContext http, long botId) =>
        bots.MayChangeAsync(db, ClientApiFilter.UserId(http), botId, http.RequestAborted);

    internal static IResult BotNotFound() =>
        ApiJson.Error(StatusCodes.Status404NotFound, "bot_not_found", "You have no bot with this id.");

    internal static string BotTarget(long botId) => string.Create(CultureInfo.InvariantCulture, $"bot {botId}");
}

/// <summary>REL-36: only an approved builder or an admin. Asked on every call, so an approval works at once.</summary>
internal sealed class BuilderFilter(IUserDirectory users) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var access = await users.GetAccessAsync(ClientApiFilter.UserId(http), http.RequestAborted);
        return access.IsBuilder
            ? await next(context)
            : ApiJson.Error(StatusCodes.Status403Forbidden, "not_builder",
                "Only an approved builder may do this. Ask to become a builder in your account screen.");
    }
}
