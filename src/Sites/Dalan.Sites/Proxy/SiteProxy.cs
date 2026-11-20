using System.Net;
using Dalan.Sites.Names;
using Dalan.Sites.Tunnel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace Dalan.Sites.Proxy;

/// <summary>SITE-08 to SITE-12: a visitor request to "name.(sites domain)" goes through the site's session and back.</summary>
internal sealed class SiteProxy(TunnelHub hub, SiteBook book, IOptionsMonitor<SitesOptions> options, TimeProvider time)
{
    /// <summary>Headers of one connection only; they never pass a proxy (RFC 9110 section 7.6.1).</summary>
    private static readonly HashSet<string> HopByHop = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection", "Keep-Alive", "Transfer-Encoding", "Upgrade", "TE", "Trailer", "Proxy-Connection",
        "Proxy-Authenticate", "Proxy-Authorization", "Content-Length", "Host",
    };

    /// <summary>
    /// The site name when the host is "name.(sites domain)"; empty for a deeper name under the sites domain;
    /// null when it is not a site host (also the sites domain itself, so "localhost" stays Dalan in local work).
    /// </summary>
    public static string? SiteOf(HostString host, SitesOptions settings)
    {
        if (settings.Domain.Length == 0) return null;
        var name = host.Host.ToLowerInvariant();
        var suffix = "." + settings.Domain.ToLowerInvariant();
        if (!name.EndsWith(suffix, StringComparison.Ordinal)) return null;
        var label = name[..^suffix.Length];
        return label.Contains('.', StringComparison.Ordinal) ? "" : label;
    }

    public async Task HandleAsync(HttpContext http, string site)
    {
        var settings = options.CurrentValue;
        var session = site.Length == 0 ? null : hub.Find(site);
        if (session is null)
        {
            var known = site.Length > 0 && await book.ExistsAsync(site, http.RequestAborted);
            await PageAsync(http, known ? StatusCodes.Status502BadGateway : StatusCodes.Status404NotFound,
                known ? "site_offline" : "site_not_found");
            return;
        }

        var body = await ReadBodyAsync(http, settings.MaxBodyBytes);
        if (body is null)
        {
            await PageAsync(http, StatusCodes.Status413PayloadTooLarge, "request_too_large");
            return;
        }

        var address = http.Connection.RemoteIpAddress;
        if (!hub.TryEnter(address))
        {
            await PageAsync(http, StatusCodes.Status429TooManyRequests, "too_many_requests");
            return;
        }

        try
        {
            await ForwardAsync(http, session, site, body, settings);
        }
        finally
        {
            hub.Leave(address);
        }
    }

    private async Task ForwardAsync(HttpContext http, TunnelSession session, string site, byte[] body, SitesOptions settings)
    {
        var (id, answer) = hub.Send(session, new TunnelRequestDraft(
            site, http.Request.Method, http.Request.Path + http.Request.QueryString, RequestHeaders(http), Base64(body)));
        TunnelResponse? response;
        try
        {
            response = await answer.WaitAsync(TimeSpan.FromSeconds(settings.RequestTimeoutSeconds), time, http.RequestAborted);
        }
        catch (TimeoutException)
        {
            await PageAsync(http, StatusCodes.Status504GatewayTimeout, "site_timeout");
            return;
        }
        catch (OperationCanceledException)
        {
            return; // The visitor left.
        }
        finally
        {
            TunnelHub.Forget(session, id);
        }

        if (response is null)
        {
            await PageAsync(http, StatusCodes.Status502BadGateway, "site_offline");
            return;
        }

        var bytes = Decode(response.Body);
        if (bytes is null || bytes.Length > settings.MaxBodyBytes || response.Status is < 100 or > 599)
        {
            await PageAsync(http, StatusCodes.Status502BadGateway, "bad_answer");
            return;
        }

        http.Response.StatusCode = response.Status;
        foreach (var (name, values) in response.Headers ?? [])
            if (!HopByHop.Contains(name) && values is not null) http.Response.Headers.Append(name, values);
        http.Response.ContentLength = bytes.Length;
        await http.Response.Body.WriteAsync(bytes, http.RequestAborted);
    }

    /// <summary>SITE-08: the visitor's headers, without hop-by-hop and forwarded ones; then the real address, scheme and host.</summary>
    private static Dictionary<string, string[]> RequestHeaders(HttpContext http)
    {
        var headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        // "Connection" may name more headers of this connection only.
        var named = http.Request.Headers.Connection.SelectMany(value => (value ?? "").Split(',', StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, values) in http.Request.Headers)
        {
            if (HopByHop.Contains(name) || named.Contains(name) || name.StartsWith("X-Forwarded-", StringComparison.OrdinalIgnoreCase)) continue;
            headers[name] = [.. values.OfType<string>()];
        }

        if (http.Connection.RemoteIpAddress is { } address) headers["X-Forwarded-For"] = [Text(address)];
        headers["X-Forwarded-Proto"] = [http.Request.Scheme];
        headers["X-Forwarded-Host"] = [http.Request.Host.Value ?? ""];
        return headers;
    }

    private static string Text(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4().ToString() : address.ToString();

    /// <summary>SITE-11: the body, or null when it is over the limit.</summary>
    private static async Task<byte[]?> ReadBodyAsync(HttpContext http, long max)
    {
        if (http.Request.ContentLength > max) return null;
        if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } size) size.MaxRequestBodySize = max + 1;

        using var memory = new MemoryStream();
        var buffer = new byte[64 * 1024];
        try
        {
            int read;
            while ((read = await http.Request.Body.ReadAsync(buffer, http.RequestAborted)) > 0)
            {
                if (memory.Length + read > max) return null;
                memory.Write(buffer, 0, read);
            }
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return null;
        }

        return memory.ToArray();
    }

    private static string? Base64(byte[] body) => body.Length == 0 ? null : Convert.ToBase64String(body);

    private static byte[]? Decode(string? body)
    {
        if (body is null) return [];
        try
        {
            return Convert.FromBase64String(body);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>SITE-09 to SITE-12: Dalan's own short page. The code is also in the header "Dalan-Error".</summary>
    private static Task PageAsync(HttpContext http, int status, string code)
    {
        http.Response.StatusCode = status;
        http.Response.Headers["Dalan-Error"] = code;
        http.Response.Headers.CacheControl = "no-store";
        http.Response.ContentType = "text/html; charset=utf-8";
        return http.Response.WriteAsync(SitePages.Render(code), http.RequestAborted);
    }
}
