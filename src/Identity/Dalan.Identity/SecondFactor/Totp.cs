using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Dalan.Identity.SecondFactor;

/// <summary>AUTH-37: RFC 6238 time-based codes (HMAC-SHA1, 6 digits) and the base32 text of the secret (RFC 4648, no padding).</summary>
public static class Totp
{
    public const int Digits = 6;

    /// <summary>20 random bytes: the size RFC 4226 recommends for HMAC-SHA1.</summary>
    public const int SecretBytes = 20;

    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>The time step of a moment: whole steps since 1970.</summary>
    public static long Step(DateTimeOffset now, int stepSeconds) => now.ToUnixTimeSeconds() / stepSeconds;

    /// <summary>The code of one time step (RFC 4226 section 5.3, with the step as the counter).</summary>
    public static string Code(byte[] secret, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);
        Span<byte> hash = stackalloc byte[20];
#pragma warning disable CA5350 // RFC 6238 and every authenticator app use HMAC-SHA1; HMAC does not rest on SHA-1's collision weakness.
        HMACSHA1.HashData(secret, counter, hash);
#pragma warning restore CA5350
        var offset = hash[^1] & 0x0f;
        var number = (BinaryPrimitives.ReadInt32BigEndian(hash[offset..]) & 0x7fffffff) % 1_000_000;
        return number.ToString("D6", CultureInfo.InvariantCulture);
    }

    public static string ToBase32(byte[] data)
    {
        var text = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = ((buffer << 8) | b) & 0xffff;
            bits += 8;
            while (bits >= 5)
            {
                text.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        if (bits > 0) text.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        return text.ToString();
    }

    public static byte[] FromBase32(string text)
    {
        var bytes = new List<byte>(text.Length * 5 / 8);
        int buffer = 0, bits = 0;
        foreach (var c in text.TrimEnd('='))
        {
            var value = Alphabet.IndexOf(char.ToUpperInvariant(c), StringComparison.Ordinal);
            if (value < 0) throw new FormatException("The text is not base32.");
            buffer = ((buffer << 5) | value) & 0xffff;
            bits += 5;
            if (bits >= 8)
            {
                bytes.Add((byte)(buffer >> (bits - 8)));
                bits -= 8;
            }
        }

        return [.. bytes];
    }
}
