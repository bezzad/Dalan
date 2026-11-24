using System.Net;
using System.Text;

namespace Dalan.Agent;

/// <summary>
/// Sends one message of an answer to the server. <c>isPart</c> is true for every message after the first.
/// False means the server no longer wants this answer (AGT-12): stop.
/// </summary>
public delegate Task<bool> AnswerSink(TunnelResponse message, bool isPart, CancellationToken cancellationToken);

/// <summary>AGT-07 and AGT-12: sends a visitor request to the app on this computer and returns its answer, in parts when it is long.</summary>
public sealed class LocalForwarder(HttpMessageHandler handler) : IDisposable
{
    /// <summary>AGT-12: a part goes out when no new byte came for this long.</summary>
    public static readonly TimeSpan FlushDelay = TimeSpan.FromMilliseconds(20);

    // The app's answer goes back as it is: no redirect is followed, no cookie kept, nothing unpacked.
    private readonly HttpClient _client = new(handler, disposeHandler: false) { Timeout = TimeSpan.FromMinutes(5) };

    public void Dispose() => _client.Dispose();

    /// <summary>The handler for real use: plain HTTP to localhost.</summary>
    public static HttpMessageHandler RealHandler() => new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        AutomaticDecompression = DecompressionMethods.None,
        UseProxy = false,
    };

    /// <summary>
    /// Forwards the request and sends the answer through <paramref name="send"/>. With <paramref name="chunkBytes"/> 0
    /// (an older server) the answer goes whole, up to <paramref name="maxBody"/>; else in parts (AGT-12).
    /// </summary>
    public async Task ForwardAsync(
        TunnelRequest request, int port, long maxBody, int chunkBytes, AnswerSink send, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(new HttpMethod(request.Method), new Uri($"http://localhost:{port}{request.Path}"));
        if (request.Body is { } body) message.Content = new ByteArrayContent(Convert.FromBase64String(body));
        foreach (var (name, values) in request.Headers)
        {
            if (message.Headers.TryAddWithoutValidation(name, values)) continue;
            message.Content ??= new ByteArrayContent([]);
            message.Content.Headers.TryAddWithoutValidation(name, values);
        }

        // Local development servers often refuse other hosts; the site's own name is in X-Forwarded-Host.
        message.Headers.Host = $"localhost:{port}";

        HttpResponseMessage answer;
        try
        {
            answer = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException)
        {
            await send(Page(request.Id, HttpStatusCode.BadGateway,
                $"The app on port {port} of the home computer does not answer. Is it running?"), false, cancellationToken);
            return;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await send(Page(request.Id, HttpStatusCode.GatewayTimeout, $"The app on port {port} did not answer in time."), false, cancellationToken);
            return;
        }

        using (answer)
        {
            var headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            // The raw values: a parsed header such as Server would come back split into its parts.
            foreach (var (name, values) in answer.Headers.NonValidated.Concat(answer.Content.Headers.NonValidated)) headers[name] = [.. values];
            var head = new TunnelResponse(request.Id, (int)answer.StatusCode, headers, null);
            await using var stream = await answer.Content.ReadAsStreamAsync(cancellationToken);
            if (chunkBytes > 0)
            {
                await SendPartsAsync(head, stream, chunkBytes, send, cancellationToken);
                return;
            }

            var bytes = await ReadAllAsync(answer.Content, stream, maxBody, cancellationToken);
            await send(bytes is null
                ? Page(request.Id, HttpStatusCode.BadGateway, $"The answer of the app on port {port} is larger than {maxBody / 1024 / 1024} MB.")
                : head with { Body = bytes.Length == 0 ? null : Convert.ToBase64String(bytes) }, false, cancellationToken);
        }
    }

    /// <summary>
    /// AGT-12: a message goes out when <paramref name="chunkBytes"/> are ready, when the answer ends, or when no new byte came
    /// for <see cref="FlushDelay"/>. So a short answer is one message, and an event of an event stream goes out at once.
    /// </summary>
    private static async Task SendPartsAsync(TunnelResponse head, Stream stream, int chunkBytes, AnswerSink send, CancellationToken cancellationToken)
    {
        using var ready = new MemoryStream();
        var buffer = new byte[chunkBytes];
        var sentHead = false;
        var read = stream.ReadAsync(buffer, cancellationToken).AsTask();
        try
        {
            while (true)
            {
                if (ready.Length > 0 && !read.IsCompleted)
                {
                    await Task.WhenAny(read, Task.Delay(FlushDelay, cancellationToken));
                    if (!read.IsCompleted)
                    {
                        if (!await FlushAsync(more: true)) return;
                        continue;
                    }
                }

                var count = await read;
                if (count == 0)
                {
                    await FlushAsync(more: false);
                    return;
                }

                ready.Write(buffer, 0, count);
                if (ready.Length >= chunkBytes && !await FlushAsync(more: true)) return;
                read = stream.ReadAsync(buffer, cancellationToken).AsTask();
            }
        }
        finally
        {
            // A read still running ends with the answer; its error is of no use any more.
            _ = read.ContinueWith(static task => _ = task.Exception, TaskScheduler.Default);
        }

        async Task<bool> FlushAsync(bool more)
        {
            var body = ready.Length == 0 ? null : Convert.ToBase64String(ready.GetBuffer(), 0, (int)ready.Length);
            ready.SetLength(0);
            var isPart = sentHead;
            sentHead = true;
            var message = isPart ? new TunnelResponse(head.Id, 0, null, body, more) : head with { Body = body, More = more };
            return await send(message, isPart, cancellationToken);
        }
    }

    private static async Task<byte[]?> ReadAllAsync(HttpContent content, Stream stream, long max, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > max) return null;
        using var memory = new MemoryStream();
        var buffer = new byte[64 * 1024];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (memory.Length + read > max) return null;
            memory.Write(buffer, 0, read);
        }

        return memory.ToArray();
    }

    /// <summary>A short page from the agent itself.</summary>
    public static TunnelResponse Page(long id, HttpStatusCode status, string text)
    {
        var html = $"<!doctype html><meta charset=\"utf-8\"><title>Dalan</title><p style=\"font-family:sans-serif\">{WebUtility.HtmlEncode(text)}</p>";
        return new TunnelResponse(id, (int)status,
            new Dictionary<string, string[]> { ["Content-Type"] = ["text/html; charset=utf-8"], ["Dalan-Error"] = ["local_app"] },
            Convert.ToBase64String(Encoding.UTF8.GetBytes(html)));
    }
}
