using System.Globalization;

namespace Dalan.Identity.OAuth;

/// <summary>What a return address is: refused, on the site's own host, on the builder's own computer, or the proxy's (SITE-26).</summary>
internal enum ReturnKind { Bad, Site, Local, Proxy }

/// <summary>
/// AUTH-40: a login may return only to the client site's own host, exactly <c>https://name.(sites domain)/...</c>, or to
/// <c>http://localhost[:port]/...</c> for a builder's tests. Checked twice: as text (an exact start, plain printable ASCII,
/// no fragment, no backslash) and as a parsed address (host, port, no user part), so no trick of encoding or parsing gets past.
/// </summary>
internal static class ReturnAddress
{
    /// <summary>SITE-26: the proxy's own return path on a site host. The proxy takes it; it never reaches the site.</summary>
    public const string ProxyCallback = "/.dalan/callback";

    private const string Reserved = "/.dalan/";
    private const string Localhost = "http://localhost";

    /// <param name="siteUrl">The site's address with no slash at the end, from Sites (for example https://shop.dalans.ir).</param>
    public static ReturnKind Check(string? redirect, string siteUrl)
    {
        if (string.IsNullOrEmpty(redirect) || !redirect.All(IsPlain)) return ReturnKind.Bad;

        if (redirect.StartsWith(siteUrl + "/", StringComparison.Ordinal))
        {
            if (!Uri.TryCreate(redirect, UriKind.Absolute, out var address) || !Uri.TryCreate(siteUrl, UriKind.Absolute, out var site)
                || address.UserInfo.Length > 0 || address.Scheme != site.Scheme || address.Port != site.Port
                || !string.Equals(address.Host, site.Host, StringComparison.OrdinalIgnoreCase))
            {
                return ReturnKind.Bad;
            }

            if (redirect == siteUrl + ProxyCallback) return ReturnKind.Proxy;
            // The proxy's paths are its own, also when written in another way ("/x/../.dalan/", "%2Edalan").
            var path = Uri.UnescapeDataString(address.AbsolutePath);
            return path.StartsWith(Reserved, StringComparison.OrdinalIgnoreCase)
                || redirect[siteUrl.Length..].StartsWith(Reserved, StringComparison.OrdinalIgnoreCase)
                ? ReturnKind.Bad
                : ReturnKind.Site;
        }

        if (!redirect.StartsWith(Localhost, StringComparison.Ordinal)) return ReturnKind.Bad;
        var rest = redirect[Localhost.Length..];
        if (rest.StartsWith(':'))
        {
            var digits = rest[1..].TakeWhile(char.IsAsciiDigit).Count();
            if (digits is 0 or > 5 || !int.TryParse(rest.AsSpan(1, digits), NumberStyles.None, CultureInfo.InvariantCulture, out var port)
                || port is < 1 or > 65535)
            {
                return ReturnKind.Bad;
            }

            rest = rest[(1 + digits)..];
        }

        return rest.StartsWith('/') && Uri.TryCreate(redirect, UriKind.Absolute, out var local)
            && local.Scheme == Uri.UriSchemeHttp && local.Host == "localhost" && local.UserInfo.Length == 0
            ? ReturnKind.Local
            : ReturnKind.Bad;
    }

    /// <summary>Printable ASCII with none of the characters that browsers read in other ways.</summary>
    private static bool IsPlain(char c) => c is > ' ' and < '\u007f' and not ('\\' or '#' or '"' or '<' or '>' or '`' or '{' or '}' or '|' or '^');
}
