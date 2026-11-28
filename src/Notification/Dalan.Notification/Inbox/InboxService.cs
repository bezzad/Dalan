using System.Globalization;
using System.Net;
using Dalan.Admin.Contracts;
using Dalan.Identity.Contracts;
using Dalan.Notification.Contracts;
using Dalan.Notification.Data;
using Dalan.Notification.Push;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dalan.Notification.Inbox;

/// <summary>NTF-12, NTF-13: stores a notice, pushes it, and sends an SMS when no push device took it.</summary>
public sealed partial class InboxService(
    IDbContextFactory<NotificationDb> dbFactory,
    WebPushService push,
    ISmsSender sms,
    IUserPhones phones,
    IAccessLog accessLog,
    IConfiguration configuration,
    IOptionsMonitor<NotificationOptions> options,
    TimeProvider time,
    IHostApplicationLifetime lifetime,
    ILogger<InboxService> logger) : IInbox
{
    public async Task AddAsync(long userId, string title, string text, string? link, CancellationToken cancellationToken)
    {
        var notice = new Notice { UserId = userId, Title = title, Text = text, Link = link, CreatedAt = time.GetUtcNow() };
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            db.Inbox.Add(notice);
            await db.SaveChangesAsync(cancellationToken);
        }

        // NTF-07, NTF-13: the push and the SMS never make the caller wait. A tap opens the link (CLI-22).
        var tag = string.Create(CultureInfo.InvariantCulture, $"inbox-{notice.Id}");
        var delivered = push.DeliverAsync(userId, new PushNote(title, text, link ?? "/dashboard", tag));
        _ = FollowUpAsync(notice, delivered);
    }

    /// <summary>NTF-13: when the push reached no device, one short SMS, within the daily limit.</summary>
    private async Task FollowUpAsync(Notice notice, Task<int> delivered)
    {
        var stopping = lifetime.ApplicationStopping;
        try
        {
            if (await delivered > 0) return;
            await SendSmsAsync(notice, stopping);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
        }
#pragma warning disable CA1031 // A failed SMS must not end the process; the notice stays in the inbox.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogSmsFailed(exception.GetType().Name);
        }
    }

    private async Task SendSmsAsync(Notice notice, CancellationToken cancellationToken)
    {
        if (await phones.PhoneAsync(notice.UserId, cancellationToken) is not { } phone) return;

        var settings = options.CurrentValue;
        var now = time.GetUtcNow();
        var dayAgo = now.AddDays(-1);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // NTF-13: the notice takes one of the day's SMS only while there is one left.
        var claimed = await db.Database.ExecuteSqlAsync($"""
            UPDATE notification.inbox SET sms_at = {now}
            WHERE id = {notice.Id} AND (
                SELECT count(*) FROM notification.inbox WHERE user_id = {notice.UserId} AND sms_at > {dayAgo}) < {settings.InboxSmsPerDay}
            """, cancellationToken);
        if (claimed == 0)
        {
            LogDailyLimit(notice.UserId);
            return;
        }

        var link = notice.Link is null ? null : (configuration["Admin:SiteUrl"] ?? "").TrimEnd('/') + notice.Link;
        if (!await sms.SendNoticeAsync(phone, SmsText(notice.Title, link, settings.InboxSmsLength), cancellationToken))
        {
            // Not sent: it does not count against the day.
            await db.Inbox.Where(n => n.Id == notice.Id).ExecuteUpdateAsync(set => set.SetProperty(n => n.SmsAt, (DateTimeOffset?)null), cancellationToken);
            return;
        }

        // ADM-04: every SMS is recorded; the server itself asked for this one.
        await accessLog.RecordSmsAsync(phone, IPAddress.Loopback, 0, cancellationToken);
    }

    /// <summary>NTF-13: the title, a space and the link, at most <paramref name="max"/> characters. The title is cut first, so the link stays whole.</summary>
    public static string SmsText(string title, string? link, int max)
    {
        if (string.IsNullOrEmpty(link)) return PushWorker.Cut(title, max);
        var room = max - link.Length - 1;
        return room < 1 ? PushWorker.Cut(link, max) : PushWorker.Cut(title, room) + " " + link;
    }

    [LoggerMessage(LogLevel.Information, "No SMS for a notice of user {UserId}: the daily limit (Notification:InboxSmsPerDay) is reached.")]
    private partial void LogDailyLimit(long userId);

    [LoggerMessage(LogLevel.Error, "The SMS of a notice failed ({Error}); the notice stays in the inbox.")]
    private partial void LogSmsFailed(string error);
}
