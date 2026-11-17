using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Dalan.Identity.Keys;

/// <summary>AUTH-21: the shape of a key, "dalan_" and 256 random bits as base64url.</summary>
internal static class KeyText
{
    public const string Prefix = "dalan_";

    /// <summary>The "amr" of a call made with a key.</summary>
    public const string Method = "key";

    public static string New() => Prefix + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>Long and random, so a fast hash is enough to store it (like a bot token).</summary>
    public static byte[] Hash(string key) => SHA256.HashData(Encoding.UTF8.GetBytes(key));
}
