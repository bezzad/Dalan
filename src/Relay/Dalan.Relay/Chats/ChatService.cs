using System.Text;
using System.Text.Json;
using Dalan.Identity.Contracts;
using Dalan.Relay.Bots;
using Dalan.Relay.ClientApi;
using Dalan.Relay.Contracts;
using Dalan.Relay.Data;
using Dalan.Relay.Files;
using Dalan.Relay.Live;
using Dalan.Relay.Outbox;
using Dalan.Relay.Queue;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Dalan.Relay.Chats;

public enum ChatError
{
    None,
    UnknownUser,
    UnknownBot,
    TextEmpty,
    TextTooLong,
    ChatNotActive,
    BadKeyboard,
    MessageNotFound,
    NotEditable,
    NoText,
    NotModified,
    FileTooBig,
    FileNotFound,
    UploadLimit,
    BotSuspended,
}

/// <summary>A file a user sends from the client (REL-28).</summary>
public sealed record UserUpload(Stream Content, string Name, string? ContentType);

/// <summary>Saves and changes the messages of a chat. Both doors of Relay (users and bots) end here.</summary>
public sealed class ChatService(
    IDbContextFactory<RelayDb> dbFactory,
    IUserDirectory users,
    IUpdateQueue queue,
    OutboxWorker outbox,
    DeliveryMarks marks,
    IMessageNotifier notifier,
    FileStore files,
    SuspendedBots suspendedBots,
    IOptionsMonitor<RelayOptions> options,
    TimeProvider time)
{
    /// <summary>A user writes to a bot, with an optional file. The first message starts the chat (REL-05).</summary>
    public async Task<(ChatError Error, Message? Message)> ReceiveFromUserAsync(
        long botId, long userId, string text, UserUpload? upload, CancellationToken cancellationToken)
    {
        if (suspendedBots.Contains(botId)) return (ChatError.BotSuspended, null);
        if (CheckText(text, upload is not null) is var error and not ChatError.None) return (error, null);

        var sender = await users.FindAsync(userId, cancellationToken);
        if (sender is null) return (ChatError.UnknownUser, null);

        var now = time.GetUtcNow();
        StoredFile? file = null;
        if (upload is not null)
        {
            (error, file) = await SaveUserFileAsync(botId, userId, upload, now, cancellationToken);
            if (error != ChatError.None) return (error, null);
        }

        Message? message;
        try
        {
            message = await SaveAsync(
                db => db.Database.SqlQuery<long>($"""
                    INSERT INTO relay.memberships (bot_id, user_id, state, started_at, last_seq, last_version, last_read_seq)
                    VALUES ({botId}, {userId}, {(int)MembershipState.Active}, {now}, 1, 0, 1)
                    ON CONFLICT (bot_id, user_id) DO UPDATE
                    SET last_seq = relay.memberships.last_seq + 1, last_read_seq = relay.memberships.last_seq + 1
                    RETURNING last_seq AS "Value"
                    """),
                new Message
                {
                    BotId = botId,
                    UserId = userId,
                    Direction = Direction.FromUser,
                    Content = new MessageContent { Text = text, File = file is null ? null : MessageFile.Of(file.ToBot()) },
                    CreatedAt = now,
                    Delivery = DeliveryState.Pending,
                },
                file,
                cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            // The membership points to a bot that does not exist.
            if (file is not null) files.Delete(file.Id);
            return (ChatError.UnknownBot, null);
        }

        // Q-01: the message is saved; only now may the bot see it.
        var replaced = queue.Enqueue(botId, QueuedUpdate.Of(message!, sender));
        await marks.MarkNotDeliveredAsync(replaced);
        return (ChatError.None, message);
    }

    /// <summary>A bot writes to a user who has started it (REL-08, REL-17, REL-20, REL-27).</summary>
    public async Task<(ChatError Error, Message? Message)> SendFromBotAsync(
        long botId, long userId, OutgoingMessage outgoing, CancellationToken cancellationToken)
    {
        if (CheckText(outgoing.Text, outgoing.File is not null) is var error and not ChatError.None) return (error, null);
        if (!IsValid(outgoing.ReplyKeyboard) || !IsValid(outgoing.InlineKeyboard)) return (ChatError.BadKeyboard, null);

        var now = time.GetUtcNow();
        StoredFile? newFile = null;
        BotFile? file = null;
        if (outgoing.File is { Content: { } content } upload)
        {
            var name = string.IsNullOrWhiteSpace(upload.Name) ? "file" : upload.Name;
            if (await files.SaveAsync(content, cancellationToken) is not var (id, size)) return (ChatError.FileTooBig, null);

            newFile = NewFile(id, botId, userId, fromBot: true, upload.Kind, name, FileStore.TypeOf(upload.ContentType, name), size, now);
            file = newFile.ToBot();
        }
        else if (outgoing.File is { FileId: { } fileId } again)
        {
            // A bot may send again any file of its own, in any of its chats; the kind is the one it asks for now.
            file = await FindFileAsync(botId, fileId, cancellationToken) is { } known
                ? known with { Kind = again.Kind }
                : null;
            if (file is null) return (ChatError.FileNotFound, null);
        }

        var message = await SaveAsync(
            db => db.Database.SqlQuery<long>($"""
                UPDATE relay.memberships SET last_seq = last_seq + 1
                WHERE bot_id = {botId} AND user_id = {userId} AND state = {(int)MembershipState.Active}
                RETURNING last_seq AS "Value"
                """),
            new Message
            {
                BotId = botId,
                UserId = userId,
                Direction = Direction.FromBot,
                Content = new MessageContent
                {
                    Text = outgoing.Text,
                    ReplyKeyboard = ReplyKeyboard.Of(outgoing.ReplyKeyboard),
                    InlineKeyboard = InlineKeyboard.Of(outgoing.InlineKeyboard),
                    File = file is null ? null : MessageFile.Of(file),
                },
                CreatedAt = now,
            },
            newFile,
            cancellationToken);

        if (message is null && newFile is not null) files.Delete(newFile.Id);
        return message is null ? (ChatError.ChatNotActive, null) : (ChatError.None, message);
    }

    /// <summary>REL-23: null text keeps the text; the inline keyboard is always replaced.</summary>
    public async Task<(ChatError Error, Message? Message)> EditAsync(
        long botId, long userId, long seq, string? text, List<List<InlineButton>>? inlineKeyboard,
        CancellationToken cancellationToken)
    {
        if (text is not null && CheckText(text, withFile: false) is var error and not ChatError.None) return (error, null);
        if (!IsValid(inlineKeyboard)) return (ChatError.BadKeyboard, null);

        Message? edited = null;
        var result = await ChangeAsync(botId, userId, async db =>
        {
            edited = await FindAsync(db, botId, userId, seq, cancellationToken);
            if (edited is null) return (ChatError.MessageNotFound, []);
            if (edited.Direction != Direction.FromBot) return (ChatError.NotEditable, []);
            if (text is not null && edited.Content.File is not null) return (ChatError.NoText, []);

            var keyboard = InlineKeyboard.Of(inlineKeyboard is { Count: > 0 } ? inlineKeyboard : null);
            var newText = text ?? edited.Content.Text;
            if (newText == edited.Content.Text && Same(keyboard, edited.Content.InlineKeyboard)) return (ChatError.NotModified, []);

            // A new content object, so EF Core writes the whole jsonb document again.
            edited.Content = new MessageContent
            {
                Text = newText,
                ReplyKeyboard = edited.Content.ReplyKeyboard,
                InlineKeyboard = keyboard,
                File = edited.Content.File,
            };
            edited.EditedAt = time.GetUtcNow();
            return (ChatError.None, [edited]);
        }, cancellationToken);

        return result == ChatError.None ? (ChatError.None, edited) : (result, null);
    }

    /// <summary>REL-24: the row stays as an empty stub with a new version.</summary>
    public Task<ChatError> DeleteAsync(long botId, long userId, long seq, CancellationToken cancellationToken) =>
        ChangeAsync(botId, userId, async db =>
        {
            var message = await FindAsync(db, botId, userId, seq, cancellationToken);
            if (message is null) return (ChatError.MessageNotFound, []);

            message.Deleted = true;
            message.Pinned = false;
            message.Content = new MessageContent { Text = "" };
            return (ChatError.None, [message]);
        }, cancellationToken);

    /// <summary>REL-25: at most one pinned message per chat.</summary>
    public Task<ChatError> PinAsync(long botId, long userId, long seq, CancellationToken cancellationToken) =>
        ChangeAsync(botId, userId, async db =>
        {
            var message = await FindAsync(db, botId, userId, seq, cancellationToken);
            if (message is null) return (ChatError.MessageNotFound, []);
            if (message.Pinned) return (ChatError.None, []);

            var changed = await PinnedAsync(db, botId, userId, cancellationToken);
            foreach (var old in changed) old.Pinned = false;
            message.Pinned = true;
            changed.Add(message);
            return (ChatError.None, changed);
        }, cancellationToken);

    public Task<ChatError> UnpinAsync(long botId, long userId, CancellationToken cancellationToken) =>
        ChangeAsync(botId, userId, async db =>
        {
            var changed = await PinnedAsync(db, botId, userId, cancellationToken);
            foreach (var old in changed) old.Pinned = false;
            return (ChatError.None, changed);
        }, cancellationToken);

    /// <summary>
    /// REL-32: moves the read position forward, never past the newest message, and tells the user's other devices (LIVE-10).
    /// Returns the unread count, or null when the user has no chat with this bot.
    /// </summary>
    public async Task<int?> ReadAsync(long botId, long userId, long seq, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var changed = await db.Memberships
            .Where(m => m.BotId == botId && m.UserId == userId)
            .ExecuteUpdateAsync(set => set.SetProperty(
                m => m.LastReadSeq,
                m => seq > m.LastReadSeq ? (seq < m.LastSeq ? seq : m.LastSeq) : m.LastReadSeq), cancellationToken);
        if (changed == 0) return null;

        var unread = await db.Memberships
            .Where(m => m.BotId == botId && m.UserId == userId)
            .Select(chat => db.Messages.Count(m => m.BotId == chat.BotId && m.UserId == chat.UserId
                && m.Direction == Direction.FromBot && !m.Deleted && m.Seq > chat.LastReadSeq))
            .FirstAsync(cancellationToken);
        notifier.Publish(new ChatRead(botId, userId, unread));
        return unread;
    }

    /// <summary>A file of this bot that has not expired, or null.</summary>
    public async Task<BotFile?> FindFileAsync(long botId, string fileId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var now = time.GetUtcNow();
        var file = await db.Files.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == fileId && f.BotId == botId && f.ExpiresAt > now, cancellationToken);
        return file?.ToBot();
    }

    private ChatError CheckText(string text, bool withFile)
    {
        var limits = options.CurrentValue;
        if (withFile) return text.Length > limits.MaxCaptionLength ? ChatError.TextTooLong : ChatError.None;

        return string.IsNullOrEmpty(text) ? ChatError.TextEmpty
            : text.Length > limits.MaxTextLength ? ChatError.TextTooLong
            : ChatError.None;
    }

    /// <summary>REL-17: rows of button texts, inside the limits.</summary>
    private bool IsValid(List<List<string>>? keyboard) =>
        keyboard is null || IsValidShape(keyboard, text => text is not null && IsValidText(text));

    /// <summary>REL-20: like a reply keyboard, and each button has either callback data or a web link.</summary>
    private bool IsValid(List<List<InlineButton>>? keyboard)
    {
        if (keyboard is null) return true;

        var limits = options.CurrentValue;
        return IsValidShape(keyboard, button => button is not null && IsValidText(button.Text) && (button.CallbackData, button.Url) switch
        {
            ({ } data, null) => Encoding.UTF8.GetByteCount(data) is var bytes && bytes > 0 && bytes <= limits.MaxCallbackDataBytes,
            (null, { } url) => url.Length <= limits.MaxButtonUrlLength
                && Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp),
            _ => false,
        });
    }

    private bool IsValidShape<T>(List<List<T>> keyboard, Predicate<T> isValid) =>
        keyboard.TrueForAll(row => row is not null)
        && keyboard.Sum(row => row.Count) <= options.CurrentValue.MaxKeyboardButtons
        && keyboard.TrueForAll(row => row.TrueForAll(isValid));

    private bool IsValidText(string text) => text is { Length: > 0 } && text.Length <= options.CurrentValue.MaxButtonTextLength;

    private static bool Same(InlineKeyboard? a, InlineKeyboard? b) =>
        JsonSerializer.Serialize(a?.ToRows()) == JsonSerializer.Serialize(b?.ToRows());

    /// <summary>REL-28: saves a user's file to disk inside the size limit and the daily limit.</summary>
    private async Task<(ChatError, StoredFile?)> SaveUserFileAsync(
        long botId, long userId, UserUpload upload, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await files.SaveAsync(upload.Content, cancellationToken) is not var (id, size)) return (ChatError.FileTooBig, null);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var dayAgo = now.AddDays(-1);
        var used = await db.Files
            .Where(f => f.UserId == userId && !f.FromBot && f.CreatedAt > dayAgo)
            .SumAsync(f => f.Size, cancellationToken);
        if (used + size > options.CurrentValue.UserUploadMbPerDay * 1024L * 1024L)
        {
            files.Delete(id);
            return (ChatError.UploadLimit, null);
        }

        var type = FileStore.TypeOf(upload.ContentType, upload.Name);
        return (ChatError.None, NewFile(id, botId, userId, fromBot: false, KindOf(type), upload.Name, type, size, now));
    }

    /// <summary>REL-28: the kind of a user's file comes from its type.</summary>
    private static FileKind KindOf(string type) => type switch
    {
        "image/jpeg" or "image/png" or "image/gif" or "image/webp" => FileKind.Photo,
        _ when type.StartsWith("audio/", StringComparison.Ordinal) => FileKind.Audio,
        _ when type.StartsWith("video/", StringComparison.Ordinal) => FileKind.Video,
        _ => FileKind.Document,
    };

    private StoredFile NewFile(
        string id, long botId, long userId, bool fromBot, FileKind kind, string name, string type, long size, DateTimeOffset now) => new()
    {
        Id = id,
        BotId = botId,
        UserId = userId,
        FromBot = fromBot,
        Kind = kind,
        Name = name,
        ContentType = type,
        Size = size,
        CreatedAt = now,
        ExpiresAt = now.AddDays(options.CurrentValue.FileRetentionDays),
    };

    /// <summary>
    /// REL-06: takes the next sequence number of the chat and inserts the message in one transaction.
    /// REL-16: the message also takes the next version of the chat.
    /// LIVE-04: the event for the live streams is written in the same transaction, so the two never disagree.
    /// The lock on the membership row makes the numbers of one chat commit in order.
    /// Returns null when the chat gave no number (no active membership).
    /// </summary>
    private async Task<Message?> SaveAsync(
        Func<RelayDb, IQueryable<long>> nextSeq, Message message, StoredFile? newFile, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var seq = await nextSeq(db).ToListAsync(cancellationToken);
        if (seq.Count == 0) return null;

        message.Seq = seq[0];
        message.Version = await NextVersionAsync(db, message.BotId, message.UserId, cancellationToken);
        db.Messages.Add(message);
        if (newFile is not null) db.Files.Add(newFile);
        db.Outbox.Add(OutboxEvent.Of(new MessageChanged(message.BotId, message.UserId, ChatMessageJson.Of(message), New: true)));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        outbox.Wake();
        return message;
    }

    /// <summary>
    /// Runs one change of existing messages in a transaction, with the chat locked, so two changes of one chat
    /// (for example two pins) never mix. Each changed message gets the next version and an event (REL-16, LIVE-02).
    /// </summary>
    private async Task<ChatError> ChangeAsync(
        long botId, long userId, Func<RelayDb, Task<(ChatError Error, List<Message> Changed)>> change,
        CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var active = await db.Database.SqlQuery<int>($"""
            SELECT 1 AS "Value" FROM relay.memberships
            WHERE bot_id = {botId} AND user_id = {userId} AND state = {(int)MembershipState.Active}
            FOR UPDATE
            """).ToListAsync(cancellationToken);
        if (active.Count == 0) return ChatError.ChatNotActive;

        var (error, changed) = await change(db);
        if (error != ChatError.None || changed.Count == 0) return error;

        foreach (var message in changed)
        {
            message.Version = await NextVersionAsync(db, botId, userId, cancellationToken);
            db.Outbox.Add(OutboxEvent.Of(new MessageChanged(botId, userId, ChatMessageJson.Of(message))));
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        outbox.Wake();
        return ChatError.None;
    }

    internal static async Task<long> NextVersionAsync(RelayDb db, long botId, long userId, CancellationToken cancellationToken) =>
        (await db.Database.SqlQuery<long>($"""
            UPDATE relay.memberships SET last_version = last_version + 1
            WHERE bot_id = {botId} AND user_id = {userId}
            RETURNING last_version AS "Value"
            """).ToListAsync(cancellationToken))[0];

    private static Task<Message?> FindAsync(RelayDb db, long botId, long userId, long seq, CancellationToken cancellationToken) =>
        db.Messages.FirstOrDefaultAsync(
            m => m.BotId == botId && m.UserId == userId && m.Seq == seq && !m.Deleted, cancellationToken);

    private static Task<List<Message>> PinnedAsync(RelayDb db, long botId, long userId, CancellationToken cancellationToken) =>
        db.Messages.Where(m => m.BotId == botId && m.UserId == userId && m.Pinned).ToListAsync(cancellationToken);
}
