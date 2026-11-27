namespace Dalan.Relay.Contracts;

/// <summary>A bot as the admin sees it (ADM-12). <see cref="Users"/>: the people who started its chat.</summary>
public sealed record AdminBot(
    long Id, string Username, string Name, long OwnerUserId, DateTimeOffset CreatedAt, DateTimeOffset? LastSeenAt, bool Suspended,
    int Users);

/// <summary>A showcase entry that waits for review (ADM-13). The icon bytes stay in Relay.</summary>
public sealed record ShowcaseRequest(
    long BotId, string Username, string Name, long OwnerUserId, bool Suspended, string Description, string Category, bool Icon,
    DateTimeOffset UpdatedAt);

/// <summary>ADM-19: counts for the admin overview.</summary>
public sealed record BotCounts(int Bots, int Online, int ShowcaseRequests);

/// <summary>What the admin door asks of Relay (ADM-12, ADM-13, REL-38, AUTH-20).</summary>
public interface IBotAdmin
{
    /// <summary>
    /// Exact id, username or owner id; an empty query finds every bot. Newest first; <paramref name="before"/> gives the next page (ADM-12).
    /// </summary>
    Task<IReadOnlyList<AdminBot>> SearchAsync(string query, long? before, int limit, CancellationToken cancellationToken);

    /// <summary>ADM-20: one bot by id (also a deleted one), or null.</summary>
    Task<AdminBot?> FindAsync(long botId, CancellationToken cancellationToken);

    /// <summary>ADM-20: the bots of one owner that are not deleted, newest first.</summary>
    Task<IReadOnlyList<AdminBot>> OwnedByAsync(long ownerUserId, CancellationToken cancellationToken);

    /// <summary>ADM-11: how many bots (not deleted) each of these users owns. A user with none is not in the answer.</summary>
    Task<IReadOnlyDictionary<long, int>> CountByOwnerAsync(IReadOnlyCollection<long> ownerUserIds, CancellationToken cancellationToken);

    /// <summary>REL-38. Null when the bot does not exist.</summary>
    Task<AdminBot?> SetSuspendedAsync(long botId, bool suspended, CancellationToken cancellationToken);

    /// <summary>ADM-13: pending entries, oldest change first.</summary>
    Task<IReadOnlyList<ShowcaseRequest>> ShowcaseRequestsAsync(int limit, CancellationToken cancellationToken);

    /// <summary>ADM-13: lists or refuses. False when the bot has no entry.</summary>
    Task<bool> ReviewShowcaseAsync(long botId, bool approve, CancellationToken cancellationToken);

    /// <summary>ADM-19: all bots, those online now (REL-33), and showcase entries that wait for review.</summary>
    Task<BotCounts> CountAsync(CancellationToken cancellationToken);

    /// <summary>AUTH-20: closes the live streams of a suspended user.</summary>
    void EndUserStreams(long userId);
}
