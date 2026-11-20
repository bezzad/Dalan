using System.Net;
using System.Text;

namespace Dalan.Agent;

/// <summary>AGT-07: sends a visitor request to the app on this computer and returns its answer.</summary>
public sealed class LocalForwarder(HttpMessageHandler handler) : IDisposable
{
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

    public async Task<TunnelResponse> ForwardAsync(TunnelRequest request, int port, long maxBody, CancellationToken cancellationToken)
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

        try
        {
            using var answer = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var bytes = await ReadAsync(answer.Content, maxBody, cancellationToken);
            if (bytes is null) return Page(request.Id, HttpStatusCode.BadGateway, $"The answer of the app on port {port} is larger than {maxBody / 1024 / 1024} MB.");

            var headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            // The raw values: a parsed header such as Server would come back split into its parts.
            foreach (var (name, values) in answer.Headers.NonValidated.Concat(answer.Content.Headers.NonValidated)) headers[name] = [.. values];
            return new TunnelResponse(request.Id, (int)answer.StatusCode, headers, bytes.Length == 0 ? null : Convert.ToBase64String(bytes));
        }
        catch (HttpRequestException)
        {
            return Page(request.Id, HttpStatusCode.BadGateway,
                $"The app on port {port} of the home computer does not answer. Is it running?");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Page(request.Id, HttpStatusCode.GatewayTimeout, $"The app on port {port} did not answer in time.");
        }
    }

    private static async Task<byte[]?> ReadAsync(HttpContent content, long max, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > max) return null;
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
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
