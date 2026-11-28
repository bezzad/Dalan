namespace Dalan.Notification.Contracts;

/// <summary>NTF-12, NTF-13: the inbox under the bell. Other services add notices here; the user reads them in the app.</summary>
public interface IInbox
{
    /// <summary>
    /// Stores a notice for the user and returns. The push to the user's devices (NTF-06) and, when none of them
    /// takes it, the SMS (NTF-13) run in the background. <paramref name="link"/> is a path on this site, like
    /// "/dashboard/admin/users/7", or null.
    /// </summary>
    Task AddAsync(long userId, string title, string text, string? link, CancellationToken cancellationToken);
}
