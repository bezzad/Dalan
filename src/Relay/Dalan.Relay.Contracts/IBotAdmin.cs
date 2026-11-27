namespace Dalan.Relay.Contracts;

/// <summary>A bot as the admin sees it (ADM-12).</summary>
public sealed record AdminBot(
    long Id, string Username, string Name, long OwnerUserId, DateTimeOffset CreatedAt, DateTimeOffset? LastSeenAt, bool Suspended);

/// <summary>A showcase entry that waits for review (ADM-13). The icon bytes stay in Relay.</summary>
public sealed record ShowcaseRequest(
    long BotId, string Username, string Name, long OwnerUserId, bool Suspended, string Description, string Category, bool Icon,
    DateTimeOffset UpdatedAt);

/// <summary>ADM-19: counts for the admin overview.</summary>
public sealed record BotCounts(int Bots, int Online, int ShowcaseRequests);

/// <summary>What the admin door asks of Relay (ADM-12, ADM-13, REL-38, AUTH-20).</summary>
public interface IBotAdmin
{
    /// <summary>Exact id, username or owner id.</summary>
    Task<IReadOnlyList<AdminBot>> SearchAsync(string query, int limit, CancellationToken cancellationToken);

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
