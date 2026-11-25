using Dalan.Identity.Contracts;
using Dalan.Sites.Data;
using Microsoft.EntityFrameworkCore;

namespace Dalan.Sites.Names;

/// <summary>AUTH-32: a user's sites and live demos are products, so an account that has them is the one a merge keeps.</summary>
public sealed class SiteHoldings(IDbContextFactory<SitesDb> dbFactory, TimeProvider time) : IUserHoldings
{
    public async Task<UserHoldings> CountAsync(long userId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var sites = await db.Sites.CountAsync(s => s.OwnerUserId == userId, cancellationToken);
        var demos = await db.Demos.CountAsync(d => d.OwnerUserId == userId && d.EndedAt == null && d.ExpiresAt > now, cancellationToken);
        return new UserHoldings(Sites: sites + demos);
    }
}
