using Dalan.Notification.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dalan.Notification.Sms;

/// <summary>NTF-01: no provider is set, so no SMS can be sent.</summary>
internal sealed partial class NoSmsSender(ILogger<NoSmsSender> logger) : ISmsSender
{
    public Task<bool> SendLoginCodeAsync(string phone, string code, CancellationToken cancellationToken)
    {
        LogNoProvider(logger);
        return Task.FromResult(false);
    }

    [LoggerMessage(LogLevel.Error, "An SMS was asked for, but Notification:SmsProvider is not set. Nobody can log in by SMS.")]
    private static partial void LogNoProvider(ILogger logger);
}

/// <summary>
/// NTF-01: writes the code to the log and sends nothing. For local work, and for tests on a server before a provider is ready:
/// outside Development only the numbers in Notification:LogSmsPhones get a code.
/// </summary>
internal sealed partial class LogSmsSender(
    ILogger<LogSmsSender> logger, IOptionsMonitor<NotificationOptions> options, IHostEnvironment environment) : ISmsSender
{
    public Task<bool> SendLoginCodeAsync(string phone, string code, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() && !IsListed(phone))
        {
            LogNotListed(logger, phone);
            return Task.FromResult(false);
        }

        LogCode(logger, phone, code);
        return Task.FromResult(true);
    }

    /// <summary>The last ten digits decide, so 09121234567 and +989121234567 are the same number.</summary>
    private bool IsListed(string phone) =>
        (options.CurrentValue.LogSmsPhones ?? "")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(listed => LastTenDigits(listed) == LastTenDigits(phone));

    private static string LastTenDigits(string phone)
    {
        var digits = new string([.. phone.Where(char.IsAsciiDigit)]);
        return digits.Length >= 10 ? digits[^10..] : digits;
    }

    [LoggerMessage(LogLevel.Information, "SMS (not sent, log sender): login code for {Phone} is {Code}")]
    private static partial void LogCode(ILogger logger, string phone, string code);

    [LoggerMessage(LogLevel.Warning, "SMS (not sent, log sender): {Phone} is not in Notification:LogSmsPhones")]
    private static partial void LogNotListed(ILogger logger, string phone);
}
