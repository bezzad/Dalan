namespace Dalan.Identity.Contracts;

/// <summary>AUTH-31, AUTH-32: what a user holds in one service. Bots and sites are products; chats are not.</summary>
public sealed record UserHoldings(int Bots = 0, int Sites = 0, int Chats = 0)
{
    public static UserHoldings operator +(UserHoldings left, UserHoldings right) =>
        new(left.Bots + right.Bots, left.Sites + right.Sites, left.Chats + right.Chats);
}

/// <summary>AUTH-32: each service that keeps data of a user counts it here, so Identity can say which account a merge keeps.</summary>
public interface IUserHoldings
{
    Task<UserHoldings> CountAsync(long userId, CancellationToken cancellationToken);
}

/// <summary>
/// AUTH-34, REL-55: user <c>fromUserId</c> was merged into <c>intoUserId</c> and is gone. Identity calls it again
/// until it returns without an error, so it must be safe to run twice.
/// </summary>
public interface IUserMergeListener
{
    Task UserMergedAsync(long fromUserId, long intoUserId, CancellationToken cancellationToken);
}
