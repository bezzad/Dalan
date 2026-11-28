namespace Dalan.Identity.Contracts;

public interface IUserDirectory
{
    /// <summary>Returns the user's public profile, or null when the id is unknown.</summary>
    Task<UserProfile?> FindAsync(long userId, CancellationToken cancellationToken);

    /// <summary>AUTH-18 to AUTH-20: what the user may do now. Asked on every admin and builder call, so a change works at once.</summary>
    Task<UserAccess> GetAccessAsync(long userId, CancellationToken cancellationToken);

    /// <summary>SITE-25: the ids of the users whose phone is in Identity:AdminPhones now, lowest first.</summary>
    Task<IReadOnlyList<long>> AdminIdsAsync(CancellationToken cancellationToken);
}

/// <summary>
/// NTF-13: one user's phone, for the SMS of a notice that push could not bring. It carries a phone number,
/// so only the Notification service may use it (AUTH-02).
/// </summary>
public interface IUserPhones
{
    /// <summary>The user's phone like +989121234567, or null for a user with no phone or no such user.</summary>
    Task<string?> PhoneAsync(long userId, CancellationToken cancellationToken);
}
