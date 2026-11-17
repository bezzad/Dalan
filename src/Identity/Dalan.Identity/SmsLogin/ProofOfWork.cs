using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dalan.Identity.Sessions;
using Microsoft.Extensions.Options;

namespace Dalan.Identity.SmsLogin;

/// <summary>
/// SEC-06: a small proof of work before each SMS, so an SMS costs the caller something.
/// The server keeps no state: a challenge is "expiry.salt.mac", and the MAC ties it to one phone.
/// </summary>
internal sealed class ProofOfWork(SessionKey key, IOptionsMonitor<IdentityOptions> options, TimeProvider time)
{
    /// <summary>Longer than any honest nonce; stops a huge input from costing us hashing time.</summary>
    private const int MaxNonceLength = 32;

    private readonly byte[] _key = key.Derive("pow");

    public int Difficulty => options.CurrentValue.PowDifficultyBits;

    public string NewChallenge(string phone)
    {
        var expires = time.GetUtcNow().AddSeconds(options.CurrentValue.PowChallengeSeconds).ToUnixTimeSeconds()
            .ToString(CultureInfo.InvariantCulture);
        var salt = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(12));
        return $"{expires}.{salt}.{Mac(phone, expires, salt)}";
    }

    /// <summary>True when we made this challenge for this phone, it has not expired, and the work was done.</summary>
    public bool Check(string phone, string? challenge, string? nonce)
    {
        if (challenge is null || nonce is null || nonce.Length > MaxNonceLength) return false;
        if (challenge.Split('.') is not [var expires, var salt, var mac]) return false;
        if (!long.TryParse(expires, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)) return false;
        if (time.GetUtcNow().ToUnixTimeSeconds() >= seconds) return false;

        var expected = Mac(phone, expires, salt);
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(mac), Encoding.UTF8.GetBytes(expected))
            && Proof.IsSolved(challenge, nonce, Difficulty);
    }

    private string Mac(string phone, string expires, string salt) =>
        Base64Url.EncodeToString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes($"{phone}|{expires}|{salt}")));
}
