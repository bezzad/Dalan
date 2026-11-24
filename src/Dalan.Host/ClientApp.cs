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

    /// <summary>The page itself (CLI-51: one address per screen). Only these addresses exist; any other unknown path is a real 404.</summary>
    public static void MapClientPage(this WebApplication app)
    {
        foreach (var path in (string[])["/", "/chats", "/bots", "/bots/new", "/bots/{username}", "/account", "/admin", "/apps", "/privacy", "/faq", "/showcase", "/guide", "/guide/bot", "/guide/site", "/@{username}"])
            app.MapGet(path, Page).DisableRateLimiting();
    }

    private const string MainManifest = "href=\"/manifest.webmanifest\"";

    private static IResult Page(HttpContext http)
    {
        http.Response.Headers.CacheControl = NoCache;

        // REL-54: a bot's page links that bot's manifest, so Install there installs the bot. Usernames are only
        // letters, digits and _, so the value can go into the page as it is.
        if (http.Request.RouteValues["username"] is string username && http.Request.Path.StartsWithSegments("/@" + username)
            && username.Length <= 64 && username.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
        {
            using var reader = new StreamReader(Files.GetFileInfo("index.html").CreateReadStream());
            var page = reader.ReadToEnd().Replace(MainManifest, $"href=\"/@{username}/manifest.webmanifest\"", StringComparison.Ordinal);
            return Results.Text(page, "text/html; charset=utf-8");
        }

        return Results.Stream(Files.GetFileInfo("index.html").CreateReadStream(), "text/html; charset=utf-8");
    }
}
