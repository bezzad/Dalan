using System.Net;

namespace Dalan.Identity.Contracts;

/// <summary>SITE-26: who came back to a site's proxy: the site's own user id (AUTH-43 <c>sub</c>) and the name. Never the phone.</summary>
public sealed record ProxyLoginUser(string Subject, string Name);

/// <summary>
/// SITE-26: the proxy is Dalan's own client of a site. It swaps the code that came back to "/.dalan/callback" inside the
/// process, with its PKCE verifier and no secret; a code made for that address works nowhere else (not at /oauth/token).
/// </summary>
public interface IProxyLogin
{
    /// <summary>
    /// The user of the code, used once; null when the code is wrong, used, old, of another site or the verifier does not match.
    /// <paramref name="address"/> and <paramref name="port"/> are the visitor's connection, for the access log (ADM-01).
    /// </summary>
    Task<ProxyLoginUser?> RedeemAsync(
        string site, string code, string verifier, string redirectUri, IPAddress? address, int port, CancellationToken cancellationToken);
}
