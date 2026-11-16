namespace Dalan.Identity.Contracts;

public interface IUserDirectory
{
    /// <summary>Returns the user's public profile, or null when the id is unknown.</summary>
    Task<UserProfile?> FindAsync(long userId, CancellationToken cancellationToken);
}
