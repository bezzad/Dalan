using System.Text.Json;
using Dalan.Relay.Live;

namespace Dalan.Relay.Outbox;

/// <summary>
/// One event that waits to be passed on (docs/ARCHITECTURE.md section 4).
/// It is written in the same transaction as the data it tells about.
/// </summary>
public sealed class OutboxEvent
{
    public long Id { get; private set; }
    public required string Type { get; set; }
    public required string Payload { get; set; }

    public static OutboxEvent Of(MessageChanged message) =>
        new() { Type = nameof(MessageChanged), Payload = JsonSerializer.Serialize(message) };

    public MessageChanged? AsMessageChanged() =>
        Type == nameof(MessageChanged) ? JsonSerializer.Deserialize<MessageChanged>(Payload) : null;
}
