using System.Net;

namespace Dalan.Relay.Contracts;

/// <summary>
/// REL-19: what a bot may do, as plain calls. The core Bot API and every adapter plugin use it,
/// so all doors share one queue and one set of rules.
/// </summary>
public interface IBotGateway
{
    /// <summary>The biggest file a bot may upload, in bytes (REL-27). A door lets a request body be this big.</summary>
    long MaxUploadBytes { get; }

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

    /// <summary>The bot writes to a user who started it (REL-08, REL-09, REL-17, REL-20, REL-27).</summary>
    Task<BotResult<BotMessage>> SendAsync(long botId, long userId, OutgoingMessage message, CancellationToken cancellationToken);

    /// <summary>REL-56: the user id a moved chat now has, or null when the chat with this user did not move.</summary>
    Task<long?> MovedToAsync(long botId, long userId, CancellationToken cancellationToken);

    /// <summary>REL-23 and REL-53.</summary>
    Task<BotResult<BotMessage>> EditAsync(long botId, long userId, long messageId, MessageEdit edit, CancellationToken cancellationToken);

    /// <summary>REL-24.</summary>
    Task<BotError> DeleteAsync(long botId, long userId, long messageId, CancellationToken cancellationToken);

    /// <summary>REL-25: pins this message and unpins the one before.</summary>
    Task<BotError> PinAsync(long botId, long userId, long messageId, CancellationToken cancellationToken);

    /// <summary>REL-25.</summary>
    Task<BotError> UnpinAsync(long botId, long userId, CancellationToken cancellationToken);

    /// <summary>REL-22.</summary>
    Task<BotError> AnswerCallbackAsync(
        long botId, string callbackId, string? text, bool showAlert, CancellationToken cancellationToken);

    /// <summary>REL-26: replaces the command list.</summary>
    Task<BotError> SetCommandsAsync(long botId, IReadOnlyList<BotCommand> commands, CancellationToken cancellationToken);

    Task<IReadOnlyList<BotCommand>> GetCommandsAsync(long botId, CancellationToken cancellationToken);

    /// <summary>REL-29: a file of this bot, or null when it does not exist or expired.</summary>
    Task<BotFile?> GetFileAsync(long botId, string fileId, CancellationToken cancellationToken);

    /// <summary>REL-29: the bytes of a file of this bot, or null. The caller disposes the stream.</summary>
    Task<Stream?> OpenFileAsync(long botId, string fileId, CancellationToken cancellationToken);

    /// <summary>REL-51: sets the bot's icon (PNG, JPEG or WebP, at most Relay:MaxBotIconKb). Gives NotAnImage or ImageTooBig when it is refused.</summary>
    Task<BotError> SetIconAsync(long botId, Stream image, CancellationToken cancellationToken);

    /// <summary>REL-51.</summary>
    Task RemoveIconAsync(long botId, CancellationToken cancellationToken);
}
