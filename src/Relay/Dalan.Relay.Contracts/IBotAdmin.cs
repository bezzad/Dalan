namespace Dalan.Relay.Contracts;

/// <summary>A bot as the admin sees it (ADM-12).</summary>
public sealed record AdminBot(
    long Id, string Username, string Name, long OwnerUserId, DateTimeOffset CreatedAt, DateTimeOffset? LastSeenAt, bool Suspended);

/// <summary>What the admin door asks of Relay (ADM-12, REL-38, AUTH-20).</summary>
public interface IBotAdmin
{
    /// <summary>Exact id, username or owner id.</summary>
    Task<IReadOnlyList<AdminBot>> SearchAsync(string query, int limit, CancellationToken cancellationToken);

    /// <summary>REL-38. Null when the bot does not exist.</summary>
    Task<AdminBot?> SetSuspendedAsync(long botId, bool suspended, CancellationToken cancellationToken);

    /// <summary>AUTH-20: closes the live streams of a suspended user.</summary>
    void EndUserStreams(long userId);
}
