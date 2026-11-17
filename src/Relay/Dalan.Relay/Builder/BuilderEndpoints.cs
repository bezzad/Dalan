using System.Globalization;
using System.Text.RegularExpressions;
using Dalan.Admin.Contracts;
using Dalan.Identity.Contracts;
using Dalan.Relay.Bots;
using Dalan.Relay.ClientApi;
using Dalan.Relay.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Dalan.Relay.Builder;

internal sealed record CreateBotRequest(string? Username, string? Name);

internal sealed record BuilderBotJson(
    long Id, string Username, string Name, DateTimeOffset CreatedAt, DateTimeOffset? LastSeen, bool Suspended);

internal sealed record BuilderBotsResponse(IEnumerable<BuilderBotJson> Bots);

internal sealed record NewTokenJson(long Id, string Username, string Name, string Token);

/// <summary>
/// REL-36, REL-37, REL-39: a builder's own bots. In the Client API group, so the session check (REL-12),
/// the access log (ADM-01) and the per-user limit (SEC-03) hold.
/// </summary>
internal static partial class BuilderEndpoints
{
    public static void MapBuilder(this RouteGroupBuilder api)
    {
        var builder = api.MapGroup("/builder").AddEndpointFilter<BuilderFilter>();
        builder.MapGet("/bots", ListAsync);
        builder.MapPost("/bots", CreateAsync);
        builder.MapPost("/bots/{botId:long}/token", RotateAsync);
    }

    private static async Task<IResult> ListAsync(HttpContext http, IDbContextFactory<RelayDb> dbFactory, BotPresence presence)
    {
        var userId = ClientApiFilter.UserId(http);
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var bots = await db.Bots.AsNoTracking()
            .Where(b => b.OwnerUserId == userId)
            .OrderBy(b => b.Id)
            .Select(b => new { b.Id, b.Username, b.Name, b.CreatedAt, b.LastSeenAt, Suspended = b.SuspendedAt != null })
            .ToListAsync(http.RequestAborted);
        return ApiJson.Ok(new BuilderBotsResponse(bots.Select(b => new BuilderBotJson(
            b.Id, b.Username, b.Name, b.CreatedAt, presence.Of(b.Id, b.LastSeenAt).LastSeen, b.Suspended))));
    }

    private static async Task<IResult> CreateAsync(
        HttpContext http, IDbContextFactory<RelayDb> dbFactory, BotRegistry bots, IAuditLog audit,
        IOptionsMonitor<RelayOptions> options)
    {
        var limits = options.CurrentValue;
        var request = await ApiJson.ReadAsync<CreateBotRequest>(http);
#pragma warning disable CA1308 // Bot usernames are stored in lower case.
        var username = request?.Username?.Trim().TrimStart('@').ToLowerInvariant() ?? "";
#pragma warning restore CA1308
        var name = request?.Name?.Trim() ?? "";
        if (!UsernameShape().IsMatch(username) || name.Length == 0 || name.Length > limits.MaxBotNameLength)
        {
            return ApiJson.BadRequest(
                $$"""Send JSON like {"username": "weather_bot", "name": "Weather"}: a username of 5 to 32 characters of a-z, 0-9 and _ that starts with a letter, and a name of 1 to {{limits.MaxBotNameLength}} characters.""");
        }

        var userId = ClientApiFilter.UserId(http);
        await using (var db = await dbFactory.CreateDbContextAsync(http.RequestAborted))
        {
            if (await db.Bots.CountAsync(b => b.OwnerUserId == userId, http.RequestAborted) >= limits.MaxBotsPerBuilder)
            {
                return ApiJson.Error(StatusCodes.Status409Conflict, "bot_limit",
                    $"You already have {limits.MaxBotsPerBuilder} bots, the most one builder may have.");
            }
        }

        try
        {
            var (bot, token) = await bots.CreateAsync(userId, username, name, http.RequestAborted);
            await audit.RecordAsync(userId, "bot.create", BotTarget(bot.Id), bot.Username, http.RequestAborted);
            return Results.Json(new NewTokenJson(bot.Id, bot.Username, bot.Name, token), ApiJson.Options,
                statusCode: StatusCodes.Status201Created);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return ApiJson.Error(StatusCodes.Status409Conflict, "username_taken", "Another bot has this username. Choose another one.");
        }
    }

    private static async Task<IResult> RotateAsync(
        long botId, HttpContext http, IDbContextFactory<RelayDb> dbFactory, BotRegistry bots, IAuditLog audit, IUserDirectory users)
    {
        var userId = ClientApiFilter.UserId(http);
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var bot = await db.Bots.AsNoTracking().FirstOrDefaultAsync(b => b.Id == botId, http.RequestAborted);
        var mayRotate = bot is not null
            && (bot.OwnerUserId == userId || (await users.GetAccessAsync(userId, http.RequestAborted)).IsAdmin);
        if (!mayRotate || await bots.RotateTokenAsync(botId, http.RequestAborted) is not { } token)
        {
            return ApiJson.Error(StatusCodes.Status404NotFound, "bot_not_found", "You have no bot with this id.");
        }

        await audit.RecordAsync(userId, "bot.token", BotTarget(botId), null, http.RequestAborted);
        return ApiJson.Ok(new NewTokenJson(bot!.Id, bot.Username, bot.Name, token));
    }

    private static string BotTarget(long botId) => string.Create(CultureInfo.InvariantCulture, $"bot {botId}");

    [GeneratedRegex("^[a-z][a-z0-9_]{4,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernameShape();
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
