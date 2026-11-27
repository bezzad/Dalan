using System.Globalization;
using Dalan.Identity.Contracts;
using Dalan.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dalan.Identity.Users;

/// <summary>ADM-11 and AUTH-18 to AUTH-20: what the admin door and the builder area ask of Identity.</summary>
internal sealed class UserAdmin(
    IDbContextFactory<IdentityDb> dbFactory,
    SuspendedUsers suspendedUsers,
    IOptionsMonitor<IdentityOptions> options,
    TimeProvider time) : IUserAdmin
{
    /// <summary>AUTH-18: read on every call, so a change of the setting works at once.</summary>
    public bool IsAdminPhone(string? phone) =>
        phone is not null && (options.CurrentValue.AdminPhones ?? "")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(p => PhoneNumber.Normalize(p) == phone);

    public async Task<UserAccess> GetAccessAsync(long userId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Phone, u.Builder, u.SuspendedAt })
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null || user.SuspendedAt is not null) return new UserAccess(false, false, user is not null);

        var isAdmin = IsAdminPhone(user.Phone);
        return new UserAccess(isAdmin, isAdmin || user.Builder == BuilderState.Approved, false);
    }

    public async Task<IReadOnlyList<AdminUser>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        var text = query.Trim();
        var phone = PhoneNumber.Normalize(text.Replace(" ", "", StringComparison.Ordinal));
        var email = EmailAddress.Normalize(text);
        var id = long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : -1;
#pragma warning disable CA1308 // Usernames are compared in lower case, as the unique index does (AUTH-01).
        var lower = text.ToLowerInvariant();
#pragma warning restore CA1308
        if (text.Length == 0) return [];

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await Shape(db.Users
#pragma warning disable CA1304, CA1311, CA1862 // Runs in PostgreSQL.
                .Where(u => u.Id == id || (phone != null && u.Phone == phone) || (email != null && u.Email == email)
                    || (u.Username != null && u.Username.ToLower() == lower)
                    || u.DisplayName.StartsWith(text))
#pragma warning restore CA1304, CA1311, CA1862
                .OrderBy(u => u.Id)
                .Take(limit))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminUser>> BuilderRequestsAsync(int limit, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await Shape(db.Users
                .Where(u => u.Builder == BuilderState.Requested)
                .OrderBy(u => u.BuilderRequestedAt).ThenBy(u => u.Id)
                .Take(limit))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminUser?> SetBuilderAsync(long userId, bool approve, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var changed = await db.Users.Where(u => u.Id == userId)
            .ExecuteUpdateAsync(set => set
                .SetProperty(u => u.Builder, approve ? BuilderState.Approved : BuilderState.Refused)
                .SetProperty(u => u.BuilderRequestedAt, (DateTimeOffset?)null), cancellationToken);
        return changed == 0 ? null : await FindAsync(db, userId, cancellationToken);
    }

    public async Task<AdminUser?> SetSuspendedAsync(long userId, bool suspended, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var at = suspended ? time.GetUtcNow() : (DateTimeOffset?)null;
        var changed = await db.Users.Where(u => u.Id == userId)
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.SuspendedAt, u => suspended ? u.SuspendedAt ?? at : null), cancellationToken);
        if (changed == 0) return null;

        // First the memory, so the session token stops before the call returns; then every device's refresh token.
        suspendedUsers.Set(userId, suspended);
        if (suspended) await db.Sessions.Where(s => s.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        return await FindAsync(db, userId, cancellationToken);
    }

    /// <summary>AUTH-04, AUTH-18, AUTH-19: what the user may see of the own account.</summary>
    public async Task<ProfileJson?> ProfileAsync(long userId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        return user is null ? null : new ProfileJson(
            user.Id, user.DisplayName, user.Username, user.Email, user.PasswordHash != null, user.Phone != null,
            IsAdminPhone(user.Phone), BuilderName(user.Builder), user.GithubId != null);
    }

    /// <summary>
    /// AUTH-39: with Identity:AutoBuilder on, a user with a verified phone and no builder state becomes a builder.
    /// "requested" and "refused" wait for the admin.
    /// </summary>
    public async Task ApproveByPhoneAsync(long userId, CancellationToken cancellationToken)
    {
        if (!options.CurrentValue.AutoBuilder) return;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Users.Where(u => u.Id == userId && u.Phone != null && u.Builder == BuilderState.None)
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.Builder, BuilderState.Approved), cancellationToken);
    }

    private static string BuilderName(BuilderState state) => state switch
    {
        BuilderState.Requested => "requested",
        BuilderState.Approved => "approved",
        BuilderState.Refused => "refused",
        _ => "none",
    };

    public string? NormalizePhone(string? input) => PhoneNumber.Normalize(input?.Replace(" ", "", StringComparison.Ordinal));

    public string? NormalizeEmail(string? input) => EmailAddress.Normalize(input);

    public async Task<AdminUser?> FindAsync(long userId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await FindAsync(db, userId, cancellationToken);
    }

    private static Task<AdminUser?> FindAsync(IdentityDb db, long userId, CancellationToken cancellationToken) =>
        Shape(db.Users.Where(u => u.Id == userId)).FirstOrDefaultAsync(cancellationToken);

    private static IQueryable<AdminUser> Shape(IQueryable<User> users) => users.AsNoTracking().Select(u => new AdminUser(
        u.Id, u.Phone, u.Email, u.DisplayName, u.Username, u.CreatedAt, u.Builder, u.BuilderRequestedAt, u.SuspendedAt != null));
}
