namespace Dalan.Sites.Contracts;

/// <summary>
/// AUTH-40, SITE-26: what Identity needs to know of a site for "Login with Dalan": its owner (SITE-16), its public address
/// ("https://name.(sites domain)", no slash at the end) and whether the proxy asks for a login.
/// </summary>
public sealed record SiteLoginInfo(long OwnerUserId, string Url, bool ProxyLogin);

/// <summary>AUTH-40: Identity asks Sites at each use, so a deleted site, or a name with a new owner, ends the old client at once.</summary>
public interface ISiteDirectory
{
    /// <summary>The site of this name (in lower case), or null when there is none.</summary>
    Task<SiteLoginInfo?> FindAsync(string name, CancellationToken cancellationToken);
}
