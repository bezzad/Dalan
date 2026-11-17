using Dalan.Relay.Bots;
using Dalan.Relay.Chats;
using Dalan.Relay.Contracts;
using Dalan.Relay.Data;
using Dalan.Relay.Queue;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dalan.Relay.BotApi;

/// <summary>REL-19: the one place where the rules for bots live. Every bot door calls it.</summary>
internal sealed class BotGateway(
    BotRegistry bots,
    RateLimits limits,
    IUpdateQueue queue,
    ChatService chats,
    IDbContextFactory<RelayDb> dbFactory,
    IOptionsMonitor<RelayOptions> options,
    TimeProvider time,
    IHostApplicationLifetime lifetime) : IBotGateway
{
    public async Task<BotAccess> EnterAsync(string? token, CancellationToken cancellationToken)
    {
        if (await bots.AuthenticateAsync(token, cancellationToken) is not { } bot) return new BotAccess(null, null);

        // After the token check, so a wrong token cannot use up a real bot's limit.
        return limits.CheckBot(bot.Id) is { } wait ? new BotAccess(null, wait) : new BotAccess(bot, null);
    }

    public async Task<IReadOnlyList<BotUpdate>?> GetUpdatesAsync(
        long botId, long offset, long? limit, long? timeoutSeconds, CancellationToken cancellationToken)
    {
        var poll = PollRequest.Of(offset, limit, timeoutSeconds, options.CurrentValue);
        var replaced = queue.BeginPoll(botId);
        using var timeout = new CancellationTokenSource(poll.Timeout, time);
        using var endOfWait = CancellationTokenSource.CreateLinkedTokenSource(
            replaced, timeout.Token, lifetime.ApplicationStopping, cancellationToken);

        while (!replaced.IsCancellationRequested)
        {
            var batch = queue.Take(botId, poll.Offset, poll.Limit);
            await RelayDb.MarkAsync(dbFactory, batch.Delivered, DeliveryState.Delivered);
            await RelayDb.MarkAsync(dbFactory, batch.NotDelivered, DeliveryState.NotDelivered);
            if (batch.Updates.Count > 0) return batch.Updates.Select(ToUpdate).ToList();

            try
            {
                await batch.Arrival.WaitAsync(endOfWait.Token);
            }
            catch (OperationCanceledException) when (!replaced.IsCancellationRequested)
            {
                // Timeout (Q-06), server stopping (Q-09) or the bot went away: nothing to give.
                return [];
            }
            catch (OperationCanceledException)
            {
            }
        }

        // Q-05: a newer poll of the same bot took over.
        return null;
    }

    public async Task<SendResult> SendMessageAsync(
        long botId, long userId, string text, List<List<string>>? replyKeyboard, CancellationToken cancellationToken)
    {
        var (error, message) = await chats.SendFromBotAsync(botId, userId, text, replyKeyboard, cancellationToken);
        return error switch
        {
            ChatError.None => new SendResult(SendError.None, new BotMessage(
                message!.Seq, message.UserId, message.Content.Text, message.CreatedAt, message.Content.ReplyKeyboard?.ToRows())),
            ChatError.TextEmpty => new SendResult(SendError.TextEmpty, null),
            ChatError.TextTooLong => new SendResult(SendError.TextTooLong, null),
            ChatError.BadKeyboard => new SendResult(SendError.BadKeyboard, null),
            _ => new SendResult(SendError.ChatNotActive, null),
        };
    }

    private static BotUpdate ToUpdate(QueuedUpdate update) => new(
        update.Id,
        update.Seq,
        new BotUser(update.From.Id, update.From.DisplayName, update.From.Username),
        update.Text,
        update.Date);
}
