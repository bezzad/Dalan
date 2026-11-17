using System.Net;
using System.Text.Json;
using System.Threading.Channels;
using Dalan.Testing;

namespace Dalan.Relay.Tests;

/// <summary>Reads a live stream (GET /api/events) the way a browser does, one event at a time.</summary>
internal sealed class EventStreamReader : IDisposable
{
    private static readonly TimeSpan Soon = TimeSpan.FromSeconds(30);
    private readonly HttpResponseMessage _response;
    private readonly StreamReader _lines;
    private readonly Channel<string> _blocks = Channel.CreateUnbounded<string>();

    private EventStreamReader(HttpResponseMessage response, StreamReader lines)
    {
        _response = response;
        _lines = lines;
        _ = ReadAllAsync();
    }

    /// <summary>
    /// Reads the whole stream in the background, block by block. A test that stops waiting must not cancel
    /// a read on the stream itself: that would close the connection.
    /// </summary>
    private async Task ReadAllAsync()
    {
        try
        {
            var block = new List<string>();
            while (await _lines.ReadLineAsync() is { } line)
            {
                if (line.Length > 0)
                {
                    block.Add(line);
                    continue;
                }

                _blocks.Writer.TryWrite(string.Join('\n', block));
                block.Clear();
            }
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException or OperationCanceledException)
        {
            // The connection is gone: for the reader that is the end of the stream.
        }

        _blocks.Writer.TryComplete();
    }

    /// <summary>Opens the stream and waits until the server has subscribed it.</summary>
    public static async Task<EventStreamReader> OpenAsync(HttpClient browser)
    {
        var response = await browser.GetAsync("/api/events", HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        var reader = new EventStreamReader(response, new StreamReader(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken)));
        Assert.Equal(": connected", await reader.NextAsync());
        return reader;
    }

    /// <summary>The next block of the stream: a comment line, or "event: ...\ndata: ...". Null when the stream has ended.</summary>
    public async Task<string?> NextAsync(TimeSpan? timeout = null)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        limit.CancelAfter(timeout ?? Soon);

        return await _blocks.Reader.WaitToReadAsync(limit.Token) && _blocks.Reader.TryRead(out var block) ? block : null;
    }

    /// <summary>The "message" part of the next "message" event that carries this text. Heartbeats and other messages are skipped.</summary>
    public async Task<JsonElement> NextMessageAsync(string text)
    {
        while (true)
        {
            var message = (await NextMessageAsync()).GetProperty("message");
            if (message.GetProperty("text").GetString() == text) return message;
        }
    }

    /// <summary>The data of the next "message" event. Heartbeats are skipped.</summary>
    public async Task<JsonElement> NextMessageAsync()
    {
        while (true)
        {
            var block = await NextAsync();
            Assert.NotNull(block);
            if (block.StartsWith(':')) continue;

            var lines = block.Split('\n');
            Assert.Equal("event: message", lines[0]);
            Assert.StartsWith("data: ", lines[1], StringComparison.Ordinal);
            return JsonSerializer.Deserialize<JsonElement>(lines[1]["data: ".Length..]);
        }
    }

    /// <summary>True when nothing arrives for a short time.</summary>
    public async Task<bool> IsQuietAsync()
    {
        try
        {
            await NextAsync(TimeSpan.FromMilliseconds(300));
            return false;
        }
        catch (OperationCanceledException)
        {
            return true;
        }
    }

    /// <summary>
    /// Moves the test clock forward in steps until the stream gives its next block. The server may not have set
    /// its timer yet when the clock first moves, so one step is not always enough.
    /// </summary>
    public async Task<string?> NextAfterAsync(DalanApp app, TimeSpan step)
    {
        var next = NextAsync();
        for (var i = 0; i < 100 && !next.IsCompleted; i++)
        {
            app.Time.Advance(step);
            await Task.WhenAny(next, Task.Delay(100, TestContext.Current.CancellationToken));
        }

        return await next;
    }

    public void Dispose()
    {
        _lines.Dispose();
        _response.Dispose();
    }
}
