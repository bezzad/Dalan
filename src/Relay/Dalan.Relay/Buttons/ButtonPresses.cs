using System.Collections.Concurrent;
using System.Globalization;
using Dalan.Identity.Contracts;
using Dalan.Relay.Chats;
using Dalan.Relay.Data;
using Dalan.Relay.Live;
using Dalan.Relay.Queue;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dalan.Relay.Buttons;

/// <summary>
/// REL-21 and REL-22: presses on inline buttons and the bot's answers. A press is not a chat message:
/// it lives only in the bot's queue, and here until the bot answers or the answer time is over.
/// </summary>
public sealed class ButtonPresses(
    IDbContextFactory<RelayDb> dbFactory,
    IUserDirectory users,
    IUpdateQueue queue,
    IMessageNotifier notifier,
    IOptionsMonitor<RelayOptions> options,
    TimeProvider time)
{
    private sealed record Press(long BotId, long UserId, DateTimeOffset At);

    private readonly ConcurrentDictionary<string, Press> _presses = new(StringComparer.Ordinal);

    /// <summary>Queues the press for the bot and returns its id, or null when that message has no such button.</summary>
    public async Task<string?> PressAsync(long botId, long userId, long seq, string data, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var message = await db.Messages.AsNoTracking().FirstOrDefaultAsync(
            m => m.BotId == botId && m.UserId == userId && m.Seq == seq && !m.Deleted && m.Direction == Direction.FromBot,
            cancellationToken);

        // The data must be on a button of that message, so a client cannot send the bot data it never offered.
        if (message?.Content.InlineKeyboard?.Rows.Exists(row => row.Buttons.Exists(b => b.Data == data)) != true) return null;
        if (await users.FindAsync(userId, cancellationToken) is not { } sender) return null;

        // The same sequence as message ids, so update ids keep rising and offsets stay valid.
        var id = (await db.Database
            .SqlQuery<long>($"SELECT nextval(pg_get_serial_sequence('relay.messages', 'id')) AS \"Value\"")
            .ToListAsync(cancellationToken))[0];
        var now = time.GetUtcNow();
        var callbackId = id.ToString(CultureInfo.InvariantCulture);
        _presses[callbackId] = new Press(botId, userId, now);

        var replaced = queue.Enqueue(botId, new QueuedUpdate(id, sender, now, message.ToBot(), data));
        await RelayDb.MarkAsync(dbFactory, replaced, DeliveryState.NotDelivered);
        return callbackId;
    }

    /// <summary>Sends the answer to the user's open streams. False when the press is unknown, of another bot, or too old.</summary>
    public bool Answer(long botId, string callbackId, string? text, bool showAlert)
    {
        if (!_presses.TryGetValue(callbackId, out var press) || press.BotId != botId || IsOld(press)) return false;

        notifier.Publish(new CallbackAnswered(botId, press.UserId, callbackId, text, showAlert));
        return true;
    }

    /// <summary>Forgets presses whose answer time is over. The expiry sweep calls it.</summary>
    public void RemoveOld()
    {
        foreach (var (id, press) in _presses)
        {
            if (IsOld(press)) _presses.TryRemove(id, out _);
        }
    }

    private bool IsOld(Press press) =>
        press.At + TimeSpan.FromSeconds(options.CurrentValue.CallbackAnswerSeconds) < time.GetUtcNow();
}
