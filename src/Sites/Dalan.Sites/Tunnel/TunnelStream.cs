using System.Threading.Channels;

namespace Dalan.Sites.Tunnel;

/// <summary>
/// SITE-22: one part of a streamed answer. <see cref="Written"/> is set for a part posted by long polling: the POST waits
/// for it. A part that came over WebSocket has none; the server sends an "ack" after writing it.
/// </summary>
internal sealed record StreamPart(byte[] Bytes, bool Last, TaskCompletionSource<bool>? Written);

/// <summary>SITE-22: the parts of one answer on their way to the visitor. At most the window waits here.</summary>
internal sealed class TunnelStream(int window)
{
    private readonly Channel<StreamPart> _parts = Channel.CreateBounded<StreamPart>(
        new BoundedChannelOptions(Math.Max(1, window)));

    public ChannelReader<StreamPart> Parts => _parts.Reader;

    /// <summary>False when the window is full (a bad agent) or the stream has ended.</summary>
    public bool TryAdd(StreamPart part) => _parts.Writer.TryWrite(part);

    /// <summary>Ends the stream: the reader stops, and every part still waiting is told it was not written.</summary>
    public void End()
    {
        _parts.Writer.TryComplete();
        while (_parts.Reader.TryRead(out var part)) part.Written?.TrySetResult(false);
    }
}
