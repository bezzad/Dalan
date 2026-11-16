using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Dalan.Relay.Bots;

/// <summary>A bot token is "&lt;bot id&gt;:&lt;secret&gt;". The secret is 26 random bytes as 35 base64url characters.</summary>
public static class BotToken
{
    private const int SecretBytes = 26;

    public static string NewSecret() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SecretBytes));

    public static string Format(long botId, string secret) =>
        string.Create(CultureInfo.InvariantCulture, $"{botId}:{secret}");

    /// <summary>
    /// A fast hash is right here: the secret is long and random, not a human password,
    /// and it is checked on every Bot API call.
    /// </summary>
    public static byte[] Hash(string secret) => SHA256.HashData(Encoding.UTF8.GetBytes(secret));

    public static bool TryParse(string? token, out long botId, out string secret)
    {
        botId = 0;
        secret = "";
        var colon = token?.IndexOf(':', StringComparison.Ordinal) ?? -1;
        if (colon <= 0) return false;

        secret = token![(colon + 1)..];
        return secret.Length > 0
            && long.TryParse(token.AsSpan(0, colon), NumberStyles.None, CultureInfo.InvariantCulture, out botId);
    }
}
