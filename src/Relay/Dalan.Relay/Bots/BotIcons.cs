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

        var size = ImageSize(image);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Bots.Where(b => b.Id == botId).ExecuteUpdateAsync(set => set
            .SetProperty(b => b.Icon, image)
            .SetProperty(b => b.IconType, type)
            .SetProperty(b => b.IconWidth, size?.Width)
            .SetProperty(b => b.IconHeight, size?.Height)
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
            .SetProperty(b => b.IconWidth, (int?)null)
            .SetProperty(b => b.IconHeight, (int?)null)
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

    /// <summary>
    /// REL-54: width and height from the header of a PNG (IHDR), JPEG (SOF) or WebP (VP8, VP8L, VP8X); no image package.
    /// Null when the bytes end too early or are not what the type says.
    /// </summary>
    public static (int Width, int Height)? ImageSize(ReadOnlySpan<byte> b)
    {
        (int, int)? size = IconType(b) switch
        {
            "image/png" when b.Length >= 24 && b[12..16].SequenceEqual("IHDR"u8) => (Be32(b[16..]), Be32(b[20..])),
            "image/jpeg" => JpegSize(b),
            "image/webp" => WebpSize(b),
            _ => null,
        };
        return size is { Item1: > 0, Item2: > 0 } ? (size.Value.Item1, size.Value.Item2) : null;
    }

    private static int Be32(ReadOnlySpan<byte> b) => (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];

    private static (int, int)? JpegSize(ReadOnlySpan<byte> b)
    {
        var at = 2;
        while (at + 4 <= b.Length)
        {
            if (b[at] != 0xFF) return null;
            var marker = b[at + 1];
            if (marker == 0xFF) { at++; continue; }
            if (marker is 0x01 or (>= 0xD0 and <= 0xD8)) { at += 2; continue; }
            // SOF0 to SOF15, except DHT (C4), JPG (C8) and DAC (CC): precision, then height, then width.
            if (marker is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC))
                return at + 9 <= b.Length ? ((b[at + 7] << 8) | b[at + 8], (b[at + 5] << 8) | b[at + 6]) : null;
            at += 2 + ((b[at + 2] << 8) | b[at + 3]);
        }

        return null;
    }

    private static (int, int)? WebpSize(ReadOnlySpan<byte> b)
    {
        if (b.Length < 30) return null;
        var kind = b[12..16];
        if (kind.SequenceEqual("VP8 "u8)) return (b[26] | ((b[27] & 0x3F) << 8), b[28] | ((b[29] & 0x3F) << 8));
        if (kind.SequenceEqual("VP8L"u8) && b[20] == 0x2F)
            return (1 + (b[21] | ((b[22] & 0x3F) << 8)), 1 + ((b[22] >> 6) | (b[23] << 2) | ((b[24] & 0x0F) << 10)));
        if (kind.SequenceEqual("VP8X"u8)) return (1 + (b[24] | (b[25] << 8) | (b[26] << 16)), 1 + (b[27] | (b[28] << 8) | (b[29] << 16)));
        return null;
    }
}
