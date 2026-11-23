using System.Net;
using Dalan.Admin.Contracts;
using Dalan.Relay.Bots;
using Dalan.Relay.Buttons;
using Dalan.Relay.Chats;
using Dalan.Relay.Contracts;
using Dalan.Relay.Files;
using Dalan.Relay.Queue;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dalan.Relay.BotApi;

/// <summary>REL-19: the one place where the rules for bots live. Every bot door calls it.</summary>
internal sealed class BotGateway(
    BotRegistry bots,
    IAccessLog accessLog,
    RateLimits limits,
    IUpdateQueue queue,
    ChatService chats,
    ButtonPresses presses,
    FileStore files,
    DeliveryMarks marks,
    BotPresence presence,
    BotIcons icons,
    IOptionsMonitor<RelayOptions> options,
    TimeProvider time,
    IHostApplicationLifetime lifetime) : IBotGateway
{
    public long MaxUploadBytes => files.MaxBytes;

    public async Task<BotAccess> EnterAsync(string? token, IPAddress? address, int port, CancellationToken cancellationToken)
    {
        if (await bots.AuthenticateAsync(token, cancellationToken) is not var (bot, suspended)) return new BotAccess(null, null);

        // ADM-01: every door for bots ends here, so every bot call is in the access log, also of a suspended bot.
        await accessLog.RecordAsync(AccessActor.Bot, bot.Id, address, port, cancellationToken);
        if (suspended) return new BotAccess(null, null, Suspended: true);

        // After the token check, so a wrong token cannot use up a real bot's limit.
        return limits.CheckBot(bot.Id) is { } wait ? new BotAccess(null, wait) : new BotAccess(bot, null);
    }

    public async Task<IReadOnlyList<BotUpdate>?> GetUpdatesAsync(
        long botId, long offset, long? limit, long? timeoutSeconds, CancellationToken cancellationToken)
    {
        var poll = PollRequest.Of(offset, limit, timeoutSeconds, options.CurrentValue);
        // REL-33: every bot door polls here, so the status covers them all.
        try
        {
            await presence.PollStartsAsync(botId, cancellationToken);
            return await PollAsync(botId, poll, cancellationToken);
        }
        finally
        {
            presence.PollEnds(botId);
        }
    }

    private async Task<IReadOnlyList<BotUpdate>?> PollAsync(long botId, PollRequest poll, CancellationToken cancellationToken)
    {
        var replaced = queue.BeginPoll(botId);
        using var timeout = new CancellationTokenSource(poll.Timeout, time);
        using var endOfWait = CancellationTokenSource.CreateLinkedTokenSource(
            replaced, timeout.Token, lifetime.ApplicationStopping, cancellationToken);

        while (!replaced.IsCancellationRequested)
        {
            var batch = queue.Take(botId, poll.Offset, poll.Limit);
            await marks.MarkDeliveredAsync(batch.Delivered);
            await marks.MarkNotDeliveredAsync(batch.NotDelivered);
            if (batch.Updates.Count > 0) return batch.Updates.Select(u => u.ToBot()).ToList();

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

    public async Task<BotResult<BotMessage>> SendAsync(
        long botId, long userId, OutgoingMessage message, CancellationToken cancellationToken) =>
        Result(await chats.SendFromBotAsync(botId, userId, message, cancellationToken));

    public async Task<BotResult<BotMessage>> EditAsync(
        long botId, long userId, long messageId, string? text, List<List<InlineButton>>? inlineKeyboard,
        CancellationToken cancellationToken) =>
        Result(await chats.EditAsync(botId, userId, messageId, text, inlineKeyboard, cancellationToken));

    public async Task<BotError> DeleteAsync(long botId, long userId, long messageId, CancellationToken cancellationToken) =>
        ToBot(await chats.DeleteAsync(botId, userId, messageId, cancellationToken));

    public async Task<BotError> PinAsync(long botId, long userId, long messageId, CancellationToken cancellationToken) =>
        ToBot(await chats.PinAsync(botId, userId, messageId, cancellationToken));

    public async Task<BotError> UnpinAsync(long botId, long userId, CancellationToken cancellationToken) =>
        ToBot(await chats.UnpinAsync(botId, userId, cancellationToken));

    public Task<BotError> AnswerCallbackAsync(
        long botId, string callbackId, string? text, bool showAlert, CancellationToken cancellationToken) =>
        Task.FromResult(
            text?.Length > options.CurrentValue.MaxCallbackAnswerLength ? BotError.TextTooLong
            : presses.Answer(botId, callbackId, string.IsNullOrEmpty(text) ? null : text, showAlert) ? BotError.None
            : BotError.CallbackNotFound);

    public async Task<BotError> SetCommandsAsync(long botId, IReadOnlyList<BotCommand> commands, CancellationToken cancellationToken) =>
        await bots.SetCommandsAsync(botId, commands, cancellationToken) ? BotError.None : BotError.BadCommands;

    public Task<IReadOnlyList<BotCommand>> GetCommandsAsync(long botId, CancellationToken cancellationToken) =>
        bots.GetCommandsAsync(botId, cancellationToken);

    public Task<BotFile?> GetFileAsync(long botId, string fileId, CancellationToken cancellationToken) =>
        chats.FindFileAsync(botId, fileId, cancellationToken);

    public async Task<Stream?> OpenFileAsync(long botId, string fileId, CancellationToken cancellationToken) =>
        await chats.FindFileAsync(botId, fileId, cancellationToken) is { } file ? files.Open(file.Id) : null;

    public async Task<BotError> SetIconAsync(long botId, Stream image, CancellationToken cancellationToken)
    {
        var (read, bytes) = await icons.ReadAsync(image, cancellationToken);
        var result = read == IconResult.Ok ? await icons.SetAsync(botId, bytes, cancellationToken) : read;
        return result switch
        {
            IconResult.Ok => BotError.None,
            IconResult.TooBig => BotError.ImageTooBig,
            _ => BotError.NotAnImage,
        };
    }

    public Task RemoveIconAsync(long botId, CancellationToken cancellationToken) => icons.RemoveAsync(botId, cancellationToken);

    private static BotResult<BotMessage> Result((ChatError Error, Message? Message) result) =>
        new(ToBot(result.Error), result.Message?.ToBot());

    private static BotError ToBot(ChatError error) => error switch
    {
        ChatError.None => BotError.None,
        ChatError.TextEmpty => BotError.TextEmpty,
        ChatError.TextTooLong => BotError.TextTooLong,
        ChatError.BadKeyboard => BotError.BadKeyboard,
        ChatError.MessageNotFound => BotError.MessageNotFound,
        ChatError.NotEditable => BotError.NotEditable,
        ChatError.NoText => BotError.NoText,
        ChatError.NotModified => BotError.NotModified,
        ChatError.FileTooBig => BotError.FileTooBig,
        ChatError.FileNotFound => BotError.FileNotFound,
        _ => BotError.ChatNotActive,
    };
}
