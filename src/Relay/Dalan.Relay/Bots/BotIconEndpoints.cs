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
        endpoints.MapGet("/api/bots/{botId:long}/icon.svg", JpegAsSvgAsync);
        endpoints.MapGet("/@{username}/manifest.webmanifest", ManifestAsync);
        endpoints.MapGet("/@{username}/icon", AppIconAsync);
    }

    /// <summary>REL-54: the least side of an icon fit for an app; the browsers' own least for an installed app.</summary>
    private const int LeastAppIconSide = 192;

    private sealed record AppBot(long Id, string Username, string Name, string? IconType, int IconVersion, int? IconWidth, int? IconHeight);

    /// <summary>
    /// The bot of a username that is live (not deleted, not suspended). An icon saved before the size was kept gets its size
    /// read from its header here, once, and kept.
    /// </summary>
    private static async Task<AppBot?> FindAsync(string username, RelayDb db, CancellationToken cancellationToken)
    {
#pragma warning disable CA1304, CA1311, CA1862 // This runs in PostgreSQL as lower(username), which the unique index covers.
        var bot = await db.Bots.AsNoTracking()
            .Where(b => b.Username.ToLower() == username.ToLower() && b.DeletedAt == null && b.SuspendedAt == null)
            .Select(b => new AppBot(b.Id, b.Username, b.Name, b.IconType, b.IconVersion, b.IconWidth, b.IconHeight))
            .FirstOrDefaultAsync(cancellationToken);
#pragma warning restore CA1304, CA1311, CA1862
        if (bot is not { IconType: not null, IconWidth: null }) return bot;

        var bytes = await db.Bots.AsNoTracking().Where(b => b.Id == bot.Id).Select(b => b.Icon).FirstOrDefaultAsync(cancellationToken);
        if (bytes is null) return bot;
        // 0 by 0 when the header cannot be read: kept, so the picture is not read again on every request.
        var size = BotIcons.ImageSize(bytes) ?? (0, 0);

        await db.Bots.Where(b => b.Id == bot.Id && b.IconVersion == bot.IconVersion).ExecuteUpdateAsync(set => set
            .SetProperty(b => b.IconWidth, size.Width)
            .SetProperty(b => b.IconHeight, size.Height), cancellationToken);
        return bot with { IconWidth = size.Width, IconHeight = size.Height };
    }

    private static bool FitForApp(AppBot bot) =>
        bot.IconType is not null && bot.IconWidth is { } w && bot.IconHeight is { } h && w == h && w >= LeastAppIconSide;

    /// <summary>
    /// REL-54: a web app manifest for one bot, so the browser installs that bot's chat as its own app.
    /// With an icon fit for an app the list holds only that icon: a browser picks one icon by size, and a Dalan icon beside it could win.
    /// No session and no access log row, like the icon it points to.
    /// </summary>
    private static async Task<IResult> ManifestAsync(string username, HttpContext http, IDbContextFactory<RelayDb> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var bot = await FindAsync(username, db, http.RequestAborted);
        if (bot is null) return ApiJson.Error(StatusCodes.Status404NotFound, "bot_not_found", "There is no bot with this username.");

        var home = $"/@{bot.Username}";
        // No "maskable": a bot's picture may reach its edges, and a mask would cut it.
        // Chrome takes only PNG, SVG or WebP: a JPEG goes in an SVG that holds it.
        var jpeg = bot.IconType == "image/jpeg";
        List<ManifestIcon> icons = FitForApp(bot)
            ? [new ManifestIcon($"/api/bots/{bot.Id}/icon{(jpeg ? ".svg" : "")}?v={bot.IconVersion}", $"{bot.IconWidth}x{bot.IconHeight}",
                jpeg ? "image/svg+xml" : bot.IconType!, "any")]
            : [new ManifestIcon("/icons/icon-192.png", "192x192", "image/png", "any"), new ManifestIcon("/icons/icon-512.png", "512x512", "image/png", "any maskable")];

        http.Response.Headers.CacheControl = "public, max-age=300";
        return Results.Json(
            new BotManifest(home, bot.Name, bot.Name, "fa", "auto", home, home, "standalone", "#f4f6f5", "#0f6e5c", icons),
            ApiJson.Options,
            "application/manifest+json");
    }

    /// <summary>
    /// REL-54: one stable address for the page's icon links, so the host can swap a string without reading bot data.
    /// The bot's app icon, or Dalan's 192 icon when it has none that fits.
    /// </summary>
    private static async Task<IResult> AppIconAsync(string username, HttpContext http, IDbContextFactory<RelayDb> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var bot = await FindAsync(username, db, http.RequestAborted);
        if (bot is null) return ApiJson.Error(StatusCodes.Status404NotFound, "bot_not_found", "There is no bot with this username.");

        http.Response.Headers.CacheControl = "public, max-age=300";
        if (!FitForApp(bot)) return Results.Redirect("/icons/icon-192.png");

        var image = await db.Bots.AsNoTracking().Where(b => b.Id == bot.Id).Select(b => b.Icon).FirstOrDefaultAsync(http.RequestAborted);
        return image is null ? Results.Redirect("/icons/icon-192.png") : Image(http, image, bot.IconType!);
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

    /// <summary>
    /// REL-54: a JPEG icon as an SVG of the same size that holds it, for Chrome's install (it takes no JPEG). The bytes are not
    /// decoded or changed. The policy lets the SVG load only its own data, so no script runs even when opened as a page.
    /// </summary>
    private static async Task<IResult> JpegAsSvgAsync(long botId, HttpContext http, IDbContextFactory<RelayDb> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var icon = await db.Bots.AsNoTracking()
            .Where(b => b.Id == botId && b.DeletedAt == null && b.SuspendedAt == null && b.Icon != null && b.IconType == "image/jpeg"
                && b.IconWidth > 0 && b.IconHeight > 0)
            .Select(b => new { b.Icon, b.IconWidth, b.IconHeight })
            .FirstOrDefaultAsync(http.RequestAborted);
        if (icon is null) return NotFound();

        var (w, h) = (icon.IconWidth, icon.IconHeight);
        var svg = $"""<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}">""" +
            $"""<image width="{w}" height="{h}" href="data:image/jpeg;base64,{Convert.ToBase64String(icon.Icon!)}"/></svg>""";
        http.Response.Headers.ContentSecurityPolicy = "default-src 'none'; img-src data:; frame-ancestors 'none'";
        return Image(http, System.Text.Encoding.UTF8.GetBytes(svg), "image/svg+xml");
    }

    public static IResult Image(HttpContext http, byte[] bytes, string type)
    {
        http.Response.Headers.CacheControl = "public, max-age=300";
        return Results.Bytes(bytes, type);
    }

    public static IResult NotFound() =>
        ApiJson.Error(StatusCodes.Status404NotFound, "not_found", "This bot has no icon, or it is not available.");
}
