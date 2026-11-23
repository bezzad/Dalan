using Dalan.Relay.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dalan.Relay.Bots;

public enum IconResult
{
    Ok,
    NotAnImage,
    TooBig,
}

/// <summary>REL-51: the one icon of a bot. The builder REST call, the MCP tool and the Telegram adapter all end here.</summary>
internal sealed class BotIcons(IDbContextFactory<RelayDb> dbFactory, IOptionsMonitor<RelayOptions> options)
{
    public long MaxBytes => options.CurrentValue.MaxBotIconKb * 1024L;

    /// <summary>Checks the picture, then keeps it and raises the version.</summary>
    public async Task<IconResult> SetAsync(long botId, byte[] image, CancellationToken cancellationToken)
    {
        if (image.Length > MaxBytes) return IconResult.TooBig;
        if (IconType(image) is not { } type) return IconResult.NotAnImage;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Bots.Where(b => b.Id == botId).ExecuteUpdateAsync(set => set
            .SetProperty(b => b.Icon, image)
            .SetProperty(b => b.IconType, type)
            .SetProperty(b => b.IconVersion, b => b.IconVersion + 1), cancellationToken);
        return IconResult.Ok;
    }

    /// <summary>The version keeps growing after a removal, so a new icon never gets an old cache key; the JSON shows 0 while there is none.</summary>
    public async Task RemoveAsync(long botId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Bots.Where(b => b.Id == botId && b.IconType != null).ExecuteUpdateAsync(set => set
            .SetProperty(b => b.Icon, (byte[]?)null)
            .SetProperty(b => b.IconType, (string?)null)
            .SetProperty(b => b.IconVersion, b => b.IconVersion + 1), cancellationToken);
    }

    /// <summary>Reads a stream of at most <see cref="MaxBytes"/>; a longer one gives <see cref="IconResult.TooBig"/> without reading it all.</summary>
    public async Task<(IconResult Result, byte[] Image)> ReadAsync(Stream content, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxBytes) return (IconResult.TooBig, []);
            buffer.Write(chunk, 0, read);
        }

        return (IconResult.Ok, buffer.ToArray());
    }

    /// <summary>REL-41, REL-51: the type from the first bytes, never from the name or the header. No SVG: it can hold a script.</summary>
    public static string? IconType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])) return "image/png";
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF])) return "image/jpeg";
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }
}
