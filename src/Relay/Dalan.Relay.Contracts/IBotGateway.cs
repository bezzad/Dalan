using System.Net;

namespace Dalan.Relay.Contracts;

/// <summary>
/// REL-19: what a bot may do, as plain calls. The core Bot API and every adapter plugin use it,
/// so all doors share one queue and one set of rules.
/// </summary>
public interface IBotGateway
{
    /// <summary>
    /// Checks the token (REL-04), records the call in the access log (ADM-01), then checks the call limit of that bot (SEC-01).
    /// Call it once for each bot call, with the address and port of the connection as they are (<c>HttpContext.Connection</c>).
    /// </summary>
    Task<BotAccess> EnterAsync(string? token, IPAddress? address, int port, CancellationToken cancellationToken);

    /// <summary>
    /// One long poll (Q-03, Q-05, Q-06, Q-08). No <paramref name="limit"/> or <paramref name="timeoutSeconds"/>
    /// means the setting; values outside the settings are cut. Returns null when a newer poll of the same bot replaced this one.
    /// </summary>
    Task<IReadOnlyList<BotUpdate>?> GetUpdatesAsync(
        long botId, long offset, long? limit, long? timeoutSeconds, CancellationToken cancellationToken);

    /// <summary>The bot writes to a user who started it (REL-08, REL-09, REL-17).</summary>
    Task<SendResult> SendMessageAsync(
        long botId, long userId, string text, List<List<string>>? replyKeyboard, CancellationToken cancellationToken);
}
