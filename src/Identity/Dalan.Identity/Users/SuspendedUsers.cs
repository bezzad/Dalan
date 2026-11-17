using Dalan.Identity.Data;
using Microsoft.EntityFrameworkCore;

namespace Dalan.Identity.Users;

/// <summary>
/// AUTH-20: the ids of suspended users, in memory, so the JWT handler can refuse a session token
/// that has not expired yet with no database call. Loaded at start. With more than one server
/// this needs the UserSuspended event (docs/ARCHITECTURE.md section 7).
/// </summary>
internal sealed class SuspendedUsers(IDbContextFactory<IdentityDb> dbFactory)
{
    private readonly Lock _lock = new();
    private HashSet<long> _ids = [];

    public bool Contains(long userId)
    {
        lock (_lock) return _ids.Contains(userId);
    }

    public void Set(long userId, bool suspended)
    {
        lock (_lock)
        {
            if (suspended) _ids.Add(userId);
            else _ids.Remove(userId);
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var ids = await db.Users.Where(u => u.SuspendedAt != null).Select(u => u.Id).ToListAsync(cancellationToken);
        lock (_lock) _ids = [.. ids];
    }
}
