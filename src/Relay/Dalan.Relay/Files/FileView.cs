using System.Globalization;
using System.Security.Cryptography;
using Dalan.Admin.Contracts;
using Dalan.Relay.ClientApi;
using Dalan.Relay.Contracts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dalan.Relay.Files;

/// <summary>
/// REL-57 and SEC-16: an HTML file shown from the view host, a registered domain other than the app's
/// (like githubusercontent.com), through a short-lived link. The token is the only key: no table, no cookie.
/// </summary>
internal sealed class FileView(
    IDataProtectionProvider protection, IOptionsMonitor<RelayOptions> options, IConfiguration configuration, TimeProvider time) : IFileViewHost
{
    private readonly IDataProtector _protector = protection.CreateProtector("Relay.FileView");

    /// <summary>Relay:FileViewHost, or "view." + Sites:Domain; null when both are empty.</summary>
    public string? Host => options.CurrentValue.FileViewHost is { Length: > 0 } host ? host
        : configuration["Sites:Domain"] is { Length: > 0 } domain ? "view." + domain
        : null;

    /// <summary>The scheme of the sites (Sites:Scheme, "http" only in local work), else https.</summary>
    public string? Origin => Host is { } host
        ? $"{(configuration["Sites:Scheme"] is { Length: > 0 } scheme ? scheme : "https")}://{host}"
        : null;

    /// <summary>True when the request is for the view host (its port does not matter).</summary>
    public bool IsViewHost(HostString requested)
    {
        if (Host is not { } host) return false;
        var colon = host.LastIndexOf(':');
        return string.Equals(requested.Host, colon < 0 ? host : host[..colon], StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>SEC-16: true when the view host is Dalan's own host or a name under it.</summary>
    public static bool IsUnder(string viewHost, string? siteUrl)
    {
        if (viewHost.Length == 0 || !Uri.TryCreate(siteUrl, UriKind.Absolute, out var url)) return false;
        var colon = viewHost.LastIndexOf(':');
        var view = (colon < 0 ? viewHost : viewHost[..colon]).ToLowerInvariant();
        var dalan = url.Host.ToLowerInvariant();
        return view == dalan || view.EndsWith("." + dalan, StringComparison.Ordinal);
    }

    /// <summary>REL-57: type text/html, or no clear type and a name ending .html or .htm.</summary>
    public static bool IsHtml(BotFile file)
    {
        var type = file.ContentType.Split(';')[0].Trim();
        if (type.Equals("text/html", StringComparison.OrdinalIgnoreCase)) return true;
        return (type.Length == 0 || type.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            && (file.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || file.Name.EndsWith(".htm", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A token for this user, bot and file. The end time is inside the protected text and checked against our own clock
    /// (the time-limited protector reads the system clock, which the tests cannot move).
    /// </summary>
    public (string Token, DateTimeOffset ExpiresAt) Issue(long userId, long botId, string fileId)
    {
        var expiresAt = time.GetUtcNow().AddMinutes(options.CurrentValue.FileViewMinutes);
        var token = _protector.Protect(string.Create(CultureInfo.InvariantCulture,
            $"{userId}/{botId}/{fileId}/{expiresAt.ToUnixTimeMilliseconds()}"));
        return (token, expiresAt);
    }

    /// <summary>The token's user, bot, file and end time; null when it is not one of ours.</summary>
    public Ticket? Read(string token)
    {
        string text;
        try
        {
            text = _protector.Unprotect(token);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            return null;
        }

        var parts = text.Split('/');
        return parts.Length == 4
            && long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var userId)
            && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var botId)
            && long.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var end)
            ? new Ticket(userId, botId, parts[2], DateTimeOffset.FromUnixTimeMilliseconds(end))
            : null;
    }

    public bool IsLive(Ticket ticket) => ticket.ExpiresAt > time.GetUtcNow();

    internal sealed record Ticket(long UserId, long BotId, string FileId, DateTimeOffset ExpiresAt);

    private sealed record LinkJson(string Url, DateTimeOffset ExpiresAt);

    /// <summary>REL-57: <c>POST /api/bots/{botId}/files/{fileId}/view</c>. The Client API filter already wrote the access log.</summary>
    public static async Task<IResult> LinkAsync(long botId, string fileId, HttpContext http, UserFiles files, FileView view)
    {
        var userId = ClientApiFilter.UserId(http);
        if (await files.FindAsync(userId, botId, fileId, http.RequestAborted) is not { } file) return UserFiles.NotFound();
        if (!IsHtml(file))
            return ApiJson.Error(StatusCodes.Status400BadRequest, "not_html", "Only an HTML file opens in the viewer. Download this file instead.");
        if (view.Origin is not { } origin)
            return ApiJson.Error(StatusCodes.Status503ServiceUnavailable, "viewer_off", "The file viewer is off on this server. Download the file instead.");

        var (token, expiresAt) = view.Issue(userId, botId, file.Id);
        return ApiJson.Ok(new LinkJson($"{origin}/v/{token}", expiresAt));
    }

    /// <summary>
    /// SEC-16: everything on the view host. Only <c>GET /v/{token}</c> gives a file; every other path is 404.
    /// No cookie is read or set: the authentication middleware never runs on this host.
    /// </summary>
    public static async Task ServeAsync(HttpContext http)
    {
        var path = http.Request.Path.Value ?? "";
        if (!HttpMethods.IsGet(http.Request.Method) || !path.StartsWith("/v/", StringComparison.Ordinal) || path.Length == 3
            || path.IndexOf('/', 3) >= 0)
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var services = http.RequestServices;
        var view = services.GetRequiredService<FileView>();
        if (view.Read(path[3..]) is not { } ticket)
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        // ADM-01: the token names its user, so every answer to it is in the access log.
        await services.GetRequiredService<IAccessLog>().RecordAsync(
            AccessActor.User, ticket.UserId, http.Connection.RemoteIpAddress, http.Connection.RemotePort, http.RequestAborted);

        var live = view.IsLive(ticket);
        var file = live ? await services.GetRequiredService<UserFiles>().FindAsync(ticket.UserId, ticket.BotId, ticket.FileId, http.RequestAborted) : null;
        if (file is null || !IsHtml(file) || services.GetRequiredService<FileStore>().Open(file.Id) is not { } content)
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await using (content)
        {
            var headers = http.Response.Headers;
            headers.ContentType = "text/html; charset=utf-8";
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            headers.CacheControl = "private, no-store";
            headers.ContentSecurityPolicy =
                "sandbox allow-scripts allow-popups; default-src 'none'; script-src 'unsafe-inline' data: blob:; " +
                "style-src 'unsafe-inline' data: blob:; img-src data: blob:; font-src data:; media-src data: blob:; " +
                $"connect-src 'none'; form-action 'none'; base-uri 'none'; frame-ancestors {view.AppOrigin()}";
            if (content.CanSeek) http.Response.ContentLength = content.Length;
            await content.CopyToAsync(http.Response.Body, http.RequestAborted);
        }
    }

    /// <summary>The app's origin (Admin:SiteUrl), the only page that may frame a file; 'none' when it is not set.</summary>
    private string AppOrigin() =>
        Uri.TryCreate(configuration["Admin:SiteUrl"], UriKind.Absolute, out var site) ? site.GetLeftPart(UriPartial.Authority) : "'none'";
}
