using Dalan.Admin.Contracts;
using Dalan.Relay.Bots;
using Dalan.Relay.Builder;
using Dalan.Relay.ClientApi;
using Dalan.Relay.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dalan.Relay.Showcase;

internal sealed record ShowcaseBody(string? Description, string? Category);

/// <summary>REL-40: the entry as its builder sees it.</summary>
internal sealed record ShowcaseJson(string State, string Description, string Category, bool Icon)
{
    public static ShowcaseJson Of(ShowcaseEntry entry, bool icon) => Of(entry.State, entry.Description, entry.Category, icon);

#pragma warning disable CA1308 // Lower case names in JSON, like every other enum value of the API.
    public static ShowcaseJson Of(ShowcaseState state, string description, string category, bool icon) =>
        new(state.ToString().ToLowerInvariant(), description, category, icon);
#pragma warning restore CA1308
}

internal sealed record PublicBotJson(long Id, string Username, string Name, string Description, string Category, bool Icon, int IconVersion);

internal sealed record PublicShowcaseJson(IReadOnlyList<string> Categories, IEnumerable<PublicBotJson> Bots);

/// <summary>
/// REL-40 to REL-43. The builder part is in the builder group (session, access log, per-user limit, builder check);
/// the public part needs no session and knows no caller, so it writes no access log row (design.md, decision 3).
/// </summary>
internal static class ShowcaseEndpoints
{
    public static void MapShowcaseForBuilder(this RouteGroupBuilder builder)
    {
        builder.MapPut("/bots/{botId:long}/showcase", AskAsync);
        builder.MapDelete("/bots/{botId:long}/showcase", LeaveAsync);
        builder.MapPut("/bots/{botId:long}/showcase/icon", SetIconAsync);
        builder.MapGet("/bots/{botId:long}/showcase/icon", GetOwnIconAsync);
    }

    public static void MapPublicShowcase(this IEndpointRouteBuilder endpoints)
    {
        var showcase = endpoints.MapGroup("/api/showcase");
        showcase.MapGet("", ListAsync);
        showcase.MapGet("/{botId:long}/icon", GetIconAsync);
    }

    private static async Task<IResult> AskAsync(
        long botId, HttpContext http, IDbContextFactory<RelayDb> dbFactory, BuilderBots builderBots, IAuditLog audit,
        IOptionsMonitor<RelayOptions> options, TimeProvider time)
    {
        var limits = options.CurrentValue;
        var request = await ApiJson.ReadAsync<ShowcaseBody>(http);
        var description = request?.Description?.Trim() ?? "";
        var category = request?.Category?.Trim() ?? "";
        if (description.Length == 0 || description.Length > limits.MaxBotDescriptionLength || !limits.Categories.Contains(category))
        {
            return ApiJson.BadRequest(
                $$"""Send JSON like {"description": "Weather of your city", "category": "tools"}: a description of 1 to {{limits.MaxBotDescriptionLength}} characters and one of these categories: {{string.Join(", ", limits.Categories)}}.""");
        }

        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        if (!await BuilderEndpoints.MayChangeAsync(db, builderBots, http, botId)) return BuilderEndpoints.BotNotFound();

        var entry = await db.Showcase.FirstOrDefaultAsync(e => e.BotId == botId, http.RequestAborted);
        if (entry is null)
        {
            entry = new ShowcaseEntry { BotId = botId, Description = description, Category = category };
            db.Showcase.Add(entry);
        }

        entry.Description = description;
        entry.Category = category;
        entry.State = ShowcaseState.Pending;
        entry.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(http.RequestAborted);
        await audit.RecordAsync(ClientApiFilter.UserId(http), "showcase.request", BuilderEndpoints.BotTarget(botId), category,
            http.RequestAborted);
        var hasIcon = await db.Bots.AnyAsync(b => b.Id == botId && b.IconType != null, http.RequestAborted);
        return ApiJson.Ok(ShowcaseJson.Of(entry, hasIcon));
    }

    private static async Task<IResult> LeaveAsync(
        long botId, HttpContext http, IDbContextFactory<RelayDb> dbFactory, BuilderBots builderBots, IAuditLog audit)
    {
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        if (!await BuilderEndpoints.MayChangeAsync(db, builderBots, http, botId)) return BuilderEndpoints.BotNotFound();

        if (await db.Showcase.Where(e => e.BotId == botId).ExecuteDeleteAsync(http.RequestAborted) > 0)
        {
            await audit.RecordAsync(ClientApiFilter.UserId(http), "showcase.leave", BuilderEndpoints.BotTarget(botId), null,
                http.RequestAborted);
        }

        return Results.NoContent();
    }

    /// <summary>REL-41: another address of the bot icon (REL-51). The entry's state does not change.</summary>
    private static async Task<IResult> SetIconAsync(long botId, HttpContext http, BuilderBots bots)
    {
        var answer = await bots.SetIconAsync(ClientApiFilter.UserId(http), botId, http.Request.Body, http.RequestAborted);
        return Results.Json(answer.Body, ApiJson.Options, statusCode: answer.Status);
    }

    /// <summary>REL-41: the owner, or an admin who reviews it (ADM-13), sees the icon before it is listed.</summary>
    private static async Task<IResult> GetOwnIconAsync(
        long botId, HttpContext http, IDbContextFactory<RelayDb> dbFactory, BuilderBots builderBots)
    {
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        if (!await BuilderEndpoints.MayChangeAsync(db, builderBots, http, botId)) return BuilderEndpoints.BotNotFound();
        var icon = await db.Bots.AsNoTracking().Where(b => b.Id == botId && b.Icon != null)
            .Select(b => new { b.Icon, b.IconType }).FirstOrDefaultAsync(http.RequestAborted);
        return icon is null ? BotIconEndpoints.NotFound() : BotIconEndpoints.Image(http, icon.Icon!, icon.IconType!);
    }

    /// <summary>REL-43.</summary>
    private static async Task<IResult> ListAsync(HttpContext http, IDbContextFactory<RelayDb> dbFactory, IOptionsMonitor<RelayOptions> options)
    {
        if (!ApiJson.TryReadNumber(http.Request.Query, "after", out var after, out var error)) return ApiJson.BadRequest(error);
        var category = http.Request.Query["category"].ToString();
        var limits = options.CurrentValue;

        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var bots = await Listed(db)
            .Where(x => (after == null || x.Bot.Id > after) && (category == "" || x.Entry.Category == category))
            .OrderBy(x => x.Bot.Id)
            .Take(limits.ShowcasePageSize)
            .Select(x => new PublicBotJson(x.Bot.Id, x.Bot.Username, x.Bot.Name, x.Entry.Description, x.Entry.Category, x.Bot.IconType != null, x.Bot.IconType != null ? x.Bot.IconVersion : 0))
            .ToListAsync(http.RequestAborted);
        return ApiJson.Ok(new PublicShowcaseJson(limits.Categories, bots));
    }

    private static async Task<IResult> GetIconAsync(long botId, HttpContext http, IDbContextFactory<RelayDb> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var icon = await Listed(db).Where(x => x.Bot.Id == botId && x.Bot.Icon != null)
            .Select(x => new { x.Bot.Icon, x.Bot.IconType }).FirstOrDefaultAsync(http.RequestAborted);
        return icon is null
            ? ApiJson.Error(StatusCodes.Status404NotFound, "not_found", "This bot is not in the showcase or has no icon.")
            : BotIconEndpoints.Image(http, icon.Icon!, icon.IconType!);
    }

    /// <summary>REL-43: approved by the admin and not suspended.</summary>
    private static IQueryable<ListedBot> Listed(RelayDb db) => db.Bots.AsNoTracking()
        .Where(b => b.SuspendedAt == null)
        .Join(db.Showcase.AsNoTracking().Where(e => e.State == ShowcaseState.Listed), b => b.Id, e => e.BotId,
            (b, e) => new ListedBot { Bot = b, Entry = e });

    private sealed class ListedBot
    {
        public required Bot Bot { get; init; }
        public required ShowcaseEntry Entry { get; init; }
    }
}
