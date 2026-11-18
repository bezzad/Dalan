using System.Net;
using System.Net.Mail;
using Dalan.Notification.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dalan.Notification.Email;

/// <summary>NTF-11: one plain text mail through the SMTP server of the settings. Nothing here logs the password.</summary>
internal sealed partial class SmtpEmailSender(IOptionsMonitor<NotificationOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public bool IsOn => true;

    public async Task<bool> SendLoginCodeAsync(string email, string code, CancellationToken cancellationToken)
    {
        var smtp = options.CurrentValue.Smtp;
        using var message = new MailMessage(smtp.From, email)
        {
            Subject = $"Dalan code {code} | کد دالان",
            Body = $"""
                کد ورود شما به دالان: {code}
                این کد را به هیچ‌کس ندهید.

                Your Dalan login code: {code}
                Do not give this code to anyone.
                """,
        };
        using var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.StartTls,
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };
        if (smtp.UserName.Length > 0) client.Credentials = new NetworkCredential(smtp.UserName, smtp.Password);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(smtp.TimeoutSeconds));
            await client.SendMailAsync(message, timeout.Token);
            return true;
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException or OperationCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LogFailed(logger, exception.GetType().Name, (exception as SmtpException)?.StatusCode.ToString() ?? "-");
            return false;
        }
    }

    [LoggerMessage(LogLevel.Error, "The SMTP server did not take the login email ({Reason}, status {Status}). Check Notification:Smtp.")]
    private static partial void LogFailed(ILogger logger, string reason, string status);
}
