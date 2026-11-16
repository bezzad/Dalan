using Dalan.Identity.Contracts;
using Dalan.Identity.Data;
using Microsoft.EntityFrameworkCore;

namespace Dalan.Identity.Users;

internal sealed class UserDirectory(IDbContextFactory<IdentityDb> dbFactory) : IUserDirectory
{
    public async Task<UserProfile?> FindAsync(long userId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Users
            .Where(u => u.Id == userId)
            .Select(u => new UserProfile(u.Id, u.DisplayName, u.Username))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
