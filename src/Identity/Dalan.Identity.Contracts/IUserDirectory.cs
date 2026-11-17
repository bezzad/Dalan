namespace Dalan.Identity.Contracts;

public interface IUserDirectory
{
    /// <summary>Returns the user's public profile, or null when the id is unknown.</summary>
    Task<UserProfile?> FindAsync(long userId, CancellationToken cancellationToken);

    /// <summary>AUTH-18 to AUTH-20: what the user may do now. Asked on every admin and builder call, so a change works at once.</summary>
    Task<UserAccess> GetAccessAsync(long userId, CancellationToken cancellationToken);
}
