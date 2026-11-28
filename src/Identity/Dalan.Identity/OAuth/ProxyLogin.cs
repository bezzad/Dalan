using System.Net;
using Dalan.Admin.Contracts;
using Dalan.Identity.Contracts;
using Dalan.Identity.Data;
using Microsoft.EntityFrameworkCore;

namespace Dalan.Identity.OAuth;

/// <summary>
/// SITE-26: the proxy of a site is Dalan's own client of it. Instead of a secret, the proxy runs in this process and swaps the
/// code here, with its PKCE verifier; such a code is marked "proxy" and /oauth/token never takes it.
/// </summary>
internal sealed class ProxyLogin(OAuthStore store, OAuthKey key, IAccessLog accessLog, IDbContextFactory<IdentityDb> dbFactory) : IProxyLogin
{
    public async Task<ProxyLoginUser?> RedeemAsync(
        string site, string code, string verifier, string redirectUri, IPAddress? address, int port, CancellationToken cancellationToken)
    {
        var row = code.Length == 0 ? null : await store.FindCodeAsync(code, cancellationToken);
        if (row is null || !row.Proxy || row.ClientId != site || row.RedirectUri != redirectUri) return null;
        if (!await store.SpendCodeAsync(code, cancellationToken) || !Pkce.Matches(verifier, row.Challenge)) return null;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == row.UserId && u.SuspendedAt == null, cancellationToken);
        if (user is null) return null;

        // ADM-01: the visitor came back through this door.
        await accessLog.RecordAsync(AccessActor.User, user.Id, address, port, cancellationToken);
        return new ProxyLoginUser(key.Subject(site, user.Id), user.DisplayName);
    }
}
