using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;

namespace Dalan.Host;

/// <summary>
/// CLI-01 and CLI-02: serves the PWA. Its built files (client/dist) are embedded in this assembly,
/// so the release stays one executable.
/// </summary>
public static class ClientApp
{
    private const string NoCache = "no-cache";

    private static readonly ManifestEmbeddedFileProvider Files = new(typeof(ClientApp).Assembly, "wwwroot");

    /// <summary>Scripts, styles, font, icons, manifest and service worker.</summary>
    public static void UseClientFiles(this WebApplication app) => app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = Files,
        // The browser asks every time and gets "not changed" (ETag), so a new release is seen at the next open.
        OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = NoCache,
    });

    /// <summary>The page itself. Only these addresses exist; any other unknown path is a real 404.</summary>
    public static void MapClientPage(this WebApplication app)
    {
        foreach (var path in (string[])["/", "/bots", "/apps", "/privacy", "/faq", "/showcase", "/guide", "/guide/bot", "/guide/site", "/@{username}"])
            app.MapGet(path, Page).DisableRateLimiting();
    }

    private static IResult Page(HttpContext http)
    {
        http.Response.Headers.CacheControl = NoCache;
        return Results.Stream(Files.GetFileInfo("index.html").CreateReadStream(), "text/html; charset=utf-8");
    }
}
