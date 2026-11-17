using Dalan.Admin.Contracts;
using Dalan.Identity.Contracts;
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
    public static ShowcaseJson Of(ShowcaseEntry entry) => Of(entry.State, entry.Description, entry.Category, entry.Icon is not null);

#pragma warning disable CA1308 // Lower case names in JSON, like every other enum value of the API.
    public static ShowcaseJson Of(ShowcaseState state, string description, string category, bool icon) =>
        new(state.ToString().ToLowerInvariant(), description, category, icon);
#pragma warning restore CA1308
}

internal sealed record PublicBotJson(long Id, string Username, string Name, string Description, string Category, bool Icon);

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
        long botId, HttpContext http, IDbContextFactory<RelayDb> dbFactory, IUserDirectory users, IAuditLog audit,
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
        if (!await BuilderEndpoints.MayChangeAsync(db, users, http, botId)) return BuilderEndpoints.BotNotFound();

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
        return ApiJson.Ok(ShowcaseJson.Of(entry));
    }

    private static async Task<IResult> LeaveAsync(
        long botId, HttpContext http, IDbContextFactory<RelayDb> dbFactory, IUserDirectory users, IAuditLog audit)
    {
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        if (!await BuilderEndpoints.MayChangeAsync(db, users, http, botId)) return BuilderEndpoints.BotNotFound();

        if (await db.Showcase.Where(e => e.BotId == botId).ExecuteDeleteAsync(http.RequestAborted) > 0)
        {
            await audit.RecordAsync(ClientApiFilter.UserId(http), "showcase.leave", BuilderEndpoints.BotTarget(botId), null,
                http.RequestAborted);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> SetIconAsync(
        long botId, HttpContext http, IDbContextFactory<RelayDb> dbFactory, IUserDirectory users,
        IOptionsMonitor<RelayOptions> options, TimeProvider time)
    {
        var maxBytes = options.CurrentValue.MaxBotIconKb * 1024L;
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        if (!await BuilderEndpoints.MayChangeAsync(db, users, http, botId)) return BuilderEndpoints.BotNotFound();
        if (await db.Showcase.FirstOrDefaultAsync(e => e.BotId == botId, http.RequestAborted) is not { } entry)
            return NoEntry();

        var bytes = await ReadAtMostAsync(http, maxBytes);
        if (bytes is null || IconType(bytes) is not { } type)
        {
            return ApiJson.Error(StatusCodes.Status400BadRequest, "bad_icon",
                $"Send the icon as the body: a PNG, JPEG or WebP image of at most {options.CurrentValue.MaxBotIconKb} KB.");
        }

        entry.Icon = bytes;
        entry.IconType = type;
        entry.State = ShowcaseState.Pending;
        entry.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(http.RequestAborted);
        return ApiJson.Ok(ShowcaseJson.Of(entry));
    }

    /// <summary>REL-41: the owner, or an admin who reviews it (ADM-13), sees the icon before it is listed.</summary>
    private static async Task<IResult> GetOwnIconAsync(
        long botId, HttpContext http, IDbContextFactory<RelayDb> dbFactory, IUserDirectory users)
    {
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        if (!await BuilderEndpoints.MayChangeAsync(db, users, http, botId)) return BuilderEndpoints.BotNotFound();
        var icon = await db.Showcase.AsNoTracking().Where(e => e.BotId == botId && e.Icon != null)
            .Select(e => new { e.Icon, e.IconType }).FirstOrDefaultAsync(http.RequestAborted);
        return icon is null ? NoEntry() : Image(http, icon.Icon!, icon.IconType!);
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
            .Select(x => new PublicBotJson(x.Bot.Id, x.Bot.Username, x.Bot.Name, x.Entry.Description, x.Entry.Category, x.Entry.Icon != null))
            .ToListAsync(http.RequestAborted);
        return ApiJson.Ok(new PublicShowcaseJson(limits.Categories, bots));
    }

    private static async Task<IResult> GetIconAsync(long botId, HttpContext http, IDbContextFactory<RelayDb> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var icon = await Listed(db).Where(x => x.Bot.Id == botId && x.Entry.Icon != null)
            .Select(x => new { x.Entry.Icon, x.Entry.IconType }).FirstOrDefaultAsync(http.RequestAborted);
        return icon is null
            ? ApiJson.Error(StatusCodes.Status404NotFound, "not_found", "This bot is not in the showcase or has no icon.")
            : Image(http, icon.Icon!, icon.IconType!);
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

    private static IResult Image(HttpContext http, byte[] bytes, string type)
    {
        http.Response.Headers.CacheControl = "no-cache";
        return Results.Bytes(bytes, type);
    }

    private static IResult NoEntry() => ApiJson.Error(StatusCodes.Status404NotFound, "showcase_not_found",
        "This bot has no showcase entry or no icon. Send its description and category first.");

    /// <summary>The body, or null when it is empty or longer than <paramref name="maxBytes"/>.</summary>
    private static async Task<byte[]?> ReadAtMostAsync(HttpContext http, long maxBytes)
    {
        if (http.Request.ContentLength > maxBytes) return null;
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await http.Request.Body.ReadAsync(chunk, http.RequestAborted)) > 0)
        {
            if (buffer.Length + read > maxBytes) return null;
            buffer.Write(chunk, 0, read);
        }

        return buffer.Length == 0 ? null : buffer.ToArray();
    }

    /// <summary>REL-41: the type from the first bytes, never from the name or the header. No SVG: it can hold a script.</summary>
    internal static string? IconType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])) return "image/png";
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF])) return "image/jpeg";
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }
}
