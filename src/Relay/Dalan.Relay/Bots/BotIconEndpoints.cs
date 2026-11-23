using Dalan.Relay.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Dalan.Relay.Bots;

/// <summary>REL-51: the public address of a bot's icon.</summary>
internal static class BotIconEndpoints
{
    public static void MapPublicBotIcon(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/api/bots/{botId:long}/icon", GetAsync);

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
