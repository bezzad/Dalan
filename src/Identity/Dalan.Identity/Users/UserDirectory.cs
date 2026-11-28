using Dalan.Identity.Contracts;
using Dalan.Identity.Data;
using Microsoft.EntityFrameworkCore;

namespace Dalan.Identity.Users;

internal sealed class UserDirectory(IDbContextFactory<IdentityDb> dbFactory, UserAdmin users) : IUserDirectory, IUserPhones
{
    public async Task<UserProfile?> FindAsync(long userId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Users
            .Where(u => u.Id == userId)
            .Select(u => new UserProfile(u.Id, u.DisplayName, u.Username))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<UserAccess> GetAccessAsync(long userId, CancellationToken cancellationToken) =>
        users.GetAccessAsync(userId, cancellationToken);

    /// <summary>SITE-25: a suspended admin is still listed; the admin phones are the owner's own.</summary>
    public async Task<IReadOnlyList<long>> AdminIdsAsync(CancellationToken cancellationToken)
    {
        var phones = users.AdminPhones();
        if (phones.Count == 0) return [];
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Users.Where(u => u.Phone != null && phones.Contains(u.Phone)).OrderBy(u => u.Id).Select(u => u.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<string?> PhoneAsync(long userId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Users.Where(u => u.Id == userId).Select(u => u.Phone).FirstOrDefaultAsync(cancellationToken);
    }
}
