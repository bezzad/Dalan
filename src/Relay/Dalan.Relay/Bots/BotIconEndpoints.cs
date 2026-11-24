using Dalan.Relay.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Dalan.Relay.Bots;

/// <summary>REL-51: the public address of a bot's icon.</summary>
internal static class BotIconEndpoints
{
    public static void MapPublicBotIcon(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/bots/{botId:long}/icon", GetAsync);
        endpoints.MapGet("/@{username}/manifest.webmanifest", ManifestAsync);
    }

    /// <summary>
    /// REL-54: a web app manifest for one bot, so the browser installs that bot's chat as its own app.
    /// No session and no access log row, like the icon it points to.
    /// </summary>
    private static async Task<IResult> ManifestAsync(string username, HttpContext http, IDbContextFactory<RelayDb> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
#pragma warning disable CA1304, CA1311, CA1862 // This runs in PostgreSQL as lower(username), which the unique index covers.
        var bot = await db.Bots.AsNoTracking()
            .Where(b => b.Username.ToLower() == username.ToLower() && b.DeletedAt == null && b.SuspendedAt == null)
            .Select(b => new { b.Id, b.Username, b.Name, b.IconType, b.IconVersion })
            .FirstOrDefaultAsync(http.RequestAborted);
#pragma warning restore CA1304, CA1311, CA1862
        if (bot is null) return ApiJson.Error(StatusCodes.Status404NotFound, "bot_not_found", "There is no bot with this username.");

        var home = $"/@{bot.Username}";
        List<ManifestIcon> icons = [];
        // The client crops icons to 512 by 512 (CLI-50); Dalan's own icons follow for a bot icon of another size.
        if (bot.IconType is { } type) icons.Add(new ManifestIcon($"/api/bots/{bot.Id}/icon?v={bot.IconVersion}", "512x512", type, "any"));
        icons.Add(new ManifestIcon("/icons/icon-192.png", "192x192", "image/png", "any"));
        icons.Add(new ManifestIcon("/icons/icon-512.png", "512x512", "image/png", "any maskable"));

        http.Response.Headers.CacheControl = "public, max-age=300";
        return Results.Json(
            new BotManifest(home, bot.Name, bot.Name, "fa", "auto", home, home, "standalone", "#f4f6f5", "#0f6e5c", icons),
            ApiJson.Options,
            "application/manifest+json");
    }

    private sealed record BotManifest(
        string Id, string Name, string ShortName, string Lang, string Dir, string StartUrl, string Scope, string Display,
        string BackgroundColor, string ThemeColor, List<ManifestIcon> Icons);

    private sealed record ManifestIcon(string Src, string Sizes, string Type, string Purpose);

    /// <summary>
    /// No session, like a Telegram profile photo. A deleted or suspended bot has none. The clients ask with ?v=icon_version,
    /// which changes with the picture; the short cache lets a suspension take the picture away within minutes.
    /// Like the public showcase, this anonymous picture door writes no access log row (design decision 9).
    /// </summary>
    private static async Task<IResult> GetAsync(long botId, HttpContext http, IDbContextFactory<RelayDb> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var icon = await db.Bots.AsNoTracking()
            .Where(b => b.Id == botId && b.DeletedAt == null && b.SuspendedAt == null && b.Icon != null)
            .Select(b => new { b.Icon, b.IconType })
            .FirstOrDefaultAsync(http.RequestAborted);
        if (icon is null) return NotFound();

        return Image(http, icon.Icon!, icon.IconType!);
    }

    public static IResult Image(HttpContext http, byte[] bytes, string type)
    {
        http.Response.Headers.CacheControl = "public, max-age=300";
        return Results.Bytes(bytes, type);
    }

    public static IResult NotFound() =>
        ApiJson.Error(StatusCodes.Status404NotFound, "not_found", "This bot has no icon, or it is not available.");
}
