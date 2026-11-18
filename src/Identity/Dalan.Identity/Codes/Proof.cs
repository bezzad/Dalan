using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Dalan.Identity.Codes;

/// <summary>SEC-06: the work itself. The client does the same counting (client/src/pow.ts).</summary>
public static class Proof
{
    /// <summary>True when SHA-256 of "challenge.nonce" starts with at least <paramref name="difficulty"/> zero bits.</summary>
    public static bool IsSolved(string challenge, string nonce, int difficulty) =>
        LeadingZeroBits(SHA256.HashData(Encoding.UTF8.GetBytes($"{challenge}.{nonce}"))) >= difficulty;

    private static int LeadingZeroBits(ReadOnlySpan<byte> hash)
    {
        var bits = 0;
        foreach (var value in hash)
        {
            if (value == 0)
            {
                bits += 8;
                continue;
            }

            // LeadingZeroCount counts in 32 bits; a byte uses the last 8.
            return bits + BitOperations.LeadingZeroCount(value) - 24;
        }

        return bits;
    }
}
