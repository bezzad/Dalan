using Dalan.Relay.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Dalan.Relay.Files;

/// <summary>SEC-12: a file download that cannot run as a page of the site.</summary>
internal static class FileResponse
{
    private static readonly string[] SafeImages = ["image/jpeg", "image/png", "image/gif", "image/webp"];

    public static IResult Of(HttpContext http, BotFile file, Stream content)
    {
        var type = file.ContentType;
        var inline = SafeImages.Contains(type)
            || type.StartsWith("audio/", StringComparison.Ordinal) || type.StartsWith("video/", StringComparison.Ordinal);

        var headers = http.Response.Headers;
        // The host already set the page policy (SEC-04); sandbox adds "no script, no forms, no same origin".
        headers.ContentSecurityPolicy = $"{headers.ContentSecurityPolicy}; sandbox";
        var disposition = new ContentDispositionHeaderValue(inline ? "inline" : "attachment");
        disposition.SetHttpFileName(file.Name);
        headers.ContentDisposition = disposition.ToString();

        // Ranges let a video or audio player jump inside the file.
        return Results.Stream(content, type, enableRangeProcessing: true);
    }
}
