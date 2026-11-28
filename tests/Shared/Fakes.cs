using System.Collections.Concurrent;
using Dalan.Notification.Contracts;
using Microsoft.Extensions.Logging;

namespace Dalan.Testing;

/// <summary>Stands in for the SMS provider: it keeps what would have been sent.</summary>
public sealed class FakeSmsSender : ISmsSender
{
    private readonly ConcurrentQueue<(string Phone, string Code)> _sent = new();

    /// <summary>Set to true to play a provider that is down.</summary>
    public bool Fails { get; set; }

    public IReadOnlyList<(string Phone, string Code)> Sent => [.. _sent];

    public Task<bool> SendLoginCodeAsync(string phone, string code, CancellationToken cancellationToken)
    {
        if (Fails) return Task.FromResult(false);
        _sent.Enqueue((phone, code));
        return Task.FromResult(true);
    }

    private readonly ConcurrentQueue<(string Phone, string Text)> _notices = new();

    /// <summary>NTF-13: the notice texts that would have been sent.</summary>
    public IReadOnlyList<(string Phone, string Text)> Notices => [.. _notices];

    public Task<bool> SendNoticeAsync(string phone, string text, CancellationToken cancellationToken)
    {
        if (Fails) return Task.FromResult(false);
        _notices.Enqueue((phone, text));
        return Task.FromResult(true);
    }
}

/// <summary>Stands in for the email provider: it keeps what would have been sent.</summary>
public sealed class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<(string Email, string Code)> _sent = new();

    /// <summary>Set to true to play a provider that is down.</summary>
    public bool Fails { get; set; }

    public bool IsOn => true;

    public IReadOnlyList<(string Email, string Code)> Sent => [.. _sent];

    public Task<bool> SendLoginCodeAsync(string email, string code, CancellationToken cancellationToken)
    {
        if (Fails) return Task.FromResult(false);
        _sent.Enqueue((email, code));
        return Task.FromResult(true);
    }
}

/// <summary>Keeps every log line of the server, so a test can read them.</summary>
public sealed class LogCollector : ILoggerProvider
{
    private readonly ConcurrentQueue<(LogLevel Level, string Text)> _lines = new();

    public IReadOnlyList<(LogLevel Level, string Text)> Lines => [.. _lines];

    public ILogger CreateLogger(string categoryName) => new Collector(_lines);

    public void Dispose()
    {
    }

    private sealed class Collector(ConcurrentQueue<(LogLevel, string)> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            lines.Enqueue((logLevel, $"{formatter(state, exception)} {exception}"));
    }
}
