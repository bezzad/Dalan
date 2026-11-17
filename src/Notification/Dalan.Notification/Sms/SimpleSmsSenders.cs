using Dalan.Notification.Contracts;
using Microsoft.Extensions.Logging;

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

/// <summary>NTF-01: for local work. Writes the code to the log and sends nothing. Development only.</summary>
internal sealed partial class LogSmsSender(ILogger<LogSmsSender> logger) : ISmsSender
{
    public Task<bool> SendLoginCodeAsync(string phone, string code, CancellationToken cancellationToken)
    {
        LogCode(logger, phone, code);
        return Task.FromResult(true);
    }

    [LoggerMessage(LogLevel.Information, "SMS (not sent, development): login code for {Phone} is {Code}")]
    private static partial void LogCode(ILogger logger, string phone, string code);
}
