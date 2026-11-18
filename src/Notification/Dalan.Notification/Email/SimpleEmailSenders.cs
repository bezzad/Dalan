using Dalan.Notification.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dalan.Notification.Email;

/// <summary>NTF-10: no provider is set, so no email can be sent.</summary>
internal sealed partial class NoEmailSender(ILogger<NoEmailSender> logger) : IEmailSender
{
    public bool IsOn => false;

    public Task<bool> SendLoginCodeAsync(string email, string code, CancellationToken cancellationToken)
    {
        LogNoProvider(logger);
        return Task.FromResult(false);
    }

    [LoggerMessage(LogLevel.Error, "A login email was asked for, but Notification:EmailProvider is not set. Nobody can log in by email.")]
    private static partial void LogNoProvider(ILogger logger);
}

/// <summary>
/// NTF-10: writes the code to the log and sends nothing. For local work, and for tests on a server before SMTP is ready:
/// outside Development only the addresses in Notification:LogEmailAddresses get a code.
/// </summary>
internal sealed partial class LogEmailSender(
    ILogger<LogEmailSender> logger, IOptionsMonitor<NotificationOptions> options, IHostEnvironment environment) : IEmailSender
{
    public bool IsOn => true;

    public Task<bool> SendLoginCodeAsync(string email, string code, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() && !IsListed(email))
        {
            LogNotListed(logger, email);
            return Task.FromResult(false);
        }

        LogCode(logger, email, code);
        return Task.FromResult(true);
    }

    private bool IsListed(string email) =>
        (options.CurrentValue.LogEmailAddresses ?? "")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Contains(email, StringComparer.OrdinalIgnoreCase);

    [LoggerMessage(LogLevel.Information, "Email (not sent, log sender): login code for {Email} is {Code}")]
    private static partial void LogCode(ILogger logger, string email, string code);

    [LoggerMessage(LogLevel.Warning, "Email (not sent, log sender): {Email} is not in Notification:LogEmailAddresses")]
    private static partial void LogNotListed(ILogger logger, string email);
}
