using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Dalan.Identity.Passwords;

/// <summary>
/// AUTH-13: Argon2id, stored as the standard PHC text: $argon2id$v=19$m=19456,t=2,p=1$salt$hash.
/// The cost is inside the text, so the settings can change later and old passwords still work.
/// </summary>
internal sealed class PasswordHasher(IOptionsMonitor<IdentityOptions> options) : IDisposable
{
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    // Each hash takes PasswordMemoryKb of memory, so only a few may run at once.
    private readonly SemaphoreSlim _slots = new(options.CurrentValue.PasswordHashConcurrency);

    public async Task<string> HashAsync(string password)
    {
        var settings = options.CurrentValue;
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = await ComputeAsync(password, salt, settings.PasswordMemoryKb, settings.PasswordIterations, HashBytes);
        return string.Create(CultureInfo.InvariantCulture,
            $"$argon2id$v=19$m={settings.PasswordMemoryKb},t={settings.PasswordIterations},p=1${Encode(salt)}${Encode(hash)}");
    }

    public async Task<bool> VerifyAsync(string stored, string password)
    {
        // "", "argon2id", "v=19", "m=..,t=..,p=1", salt, hash
        if (stored.Split('$') is not ["", "argon2id", "v=19", var cost, var salt, var hash]) return false;
        if (cost.Split(',') is not [['m', '=', .. var memory], ['t', '=', .. var iterations], "p=1"]) return false;

        var expected = Decode(hash);
        var actual = await ComputeAsync(
            password, Decode(salt),
            int.Parse(memory, CultureInfo.InvariantCulture), int.Parse(iterations, CultureInfo.InvariantCulture), expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private async Task<byte[]> ComputeAsync(string password, byte[] salt, int memoryKb, int iterations, int length)
    {
        await _slots.WaitAsync();
        try
        {
            using var argon = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                Salt = salt,
                MemorySize = memoryKb,
                Iterations = iterations,
                DegreeOfParallelism = 1,
            };
            return await argon.GetBytesAsync(length);
        }
        finally
        {
            _slots.Release();
        }
    }

    // PHC text uses base64 without padding.
    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=');

    private static byte[] Decode(string text) => Convert.FromBase64String(text.PadRight((text.Length + 3) / 4 * 4, '='));

    public void Dispose() => _slots.Dispose();
}
