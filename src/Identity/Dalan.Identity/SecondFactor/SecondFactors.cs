using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dalan.Identity.Data;
using Dalan.Identity.Sessions;
using Dalan.Identity.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dalan.Identity.SecondFactor;

/// <summary>
/// AUTH-37, SEC-15: the TOTP secret of a user (kept encrypted with a key made from Identity:SigningKey), the recovery
/// codes (kept as hashes), and the login tickets that wait for a code. Only raw SQL reads these tables.
/// </summary>
internal sealed class SecondFactors(
    IDbContextFactory<IdentityDb> dbFactory,
    SessionKey key,
    UserAdmin users,
    IOptionsMonitor<IdentityOptions> options,
    TimeProvider time)
{
    private const int NonceBytes = 12;
    private const int TagBytes = 16;

    /// <summary>True when the user has a confirmed second factor.</summary>
    public static async Task<bool> IsOnAsync(IdentityDb db, long userId, CancellationToken cancellationToken) =>
        (await db.Database.SqlQuery<bool>($"""
            SELECT EXISTS (SELECT 1 FROM identity.totp WHERE user_id = {userId} AND on_at IS NOT NULL) AS "Value"
            """).ToListAsync(cancellationToken))[0];

    public async Task<bool> IsOnAsync(long userId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await IsOnAsync(db, userId, cancellationToken);
    }

    /// <summary>A new pending secret (not on until confirmed). Replaces an earlier pending one. Returns it as base32.</summary>
    public async Task<string> StartAsync(long userId, CancellationToken cancellationToken)
    {
        var secret = RandomNumberGenerator.GetBytes(Totp.SecretBytes);
        var sealedSecret = Seal(secret);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO identity.totp (user_id, secret) VALUES ({userId}, {sealedSecret})
            ON CONFLICT (user_id) DO UPDATE SET secret = excluded.secret, last_step = NULL WHERE identity.totp.on_at IS NULL
            """, cancellationToken);
        return Totp.ToBase32(secret);
    }

    /// <summary>The first right code turns the pending secret on. Returns the new recovery codes, or null for a wrong code.</summary>
    public async Task<string[]?> ConfirmAsync(long userId, string code, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var rows = await db.Database.SqlQuery<byte[]>($"""
            SELECT secret AS "Value" FROM identity.totp WHERE user_id = {userId} AND on_at IS NULL FOR UPDATE
            """).ToListAsync(cancellationToken);
        if (rows.Count == 0 || MatchingStep(Open(rows[0]), code) is not { } step) return null;

        var now = time.GetUtcNow();
        await db.Database.ExecuteSqlAsync(
            $"UPDATE identity.totp SET on_at = {now}, last_step = {step} WHERE user_id = {userId}", cancellationToken);
        await db.Database.ExecuteSqlAsync($"DELETE FROM identity.recovery_codes WHERE user_id = {userId}", cancellationToken);
        var codes = new string[options.CurrentValue.RecoveryCodes];
        for (var i = 0; i < codes.Length; i++)
        {
            codes[i] = NewRecoveryCode();
            var hash = RecoveryHash(codes[i]);
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO identity.recovery_codes (user_id, code_hash) VALUES ({userId}, {hash})", cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return codes;
    }

    /// <summary>
    /// A right TOTP code of a step after the last used one, or an unused recovery code (which is then gone).
    /// False when the second factor is off.
    /// </summary>
    public async Task<bool> CheckAsync(long userId, string? code, CancellationToken cancellationToken)
    {
        code = code?.Trim() ?? "";
        if (code.Length == 0) return false;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (code.Length == Totp.Digits)
        {
            var rows = await db.Database.SqlQuery<byte[]>($"""
                SELECT secret AS "Value" FROM identity.totp WHERE user_id = {userId} AND on_at IS NOT NULL
                """).ToListAsync(cancellationToken);
            if (rows.Count == 0 || MatchingStep(Open(rows[0]), code) is not { } step) return false;

            // One statement: a code is taken once, even by two calls at the same moment.
            return await db.Database.ExecuteSqlAsync($"""
                UPDATE identity.totp SET last_step = {step}
                WHERE user_id = {userId} AND on_at IS NOT NULL AND (last_step IS NULL OR last_step < {step})
                """, cancellationToken) == 1;
        }

        var hash = RecoveryHash(code);
        return await db.Database.ExecuteSqlAsync($"""
            DELETE FROM identity.recovery_codes AS r USING identity.totp AS t
            WHERE r.user_id = {userId} AND r.code_hash = {hash} AND t.user_id = r.user_id AND t.on_at IS NOT NULL
            """, cancellationToken) == 1;
    }

    /// <summary>Turns the second factor off: the secret and the recovery codes are gone.</summary>
    public async Task OffAsync(long userId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM identity.recovery_codes WHERE user_id = {userId};
            DELETE FROM identity.totp WHERE user_id = {userId};
            """, cancellationToken);
    }

    /// <summary>
    /// AUTH-37, SEC-15: a ticket when this login must also give a code: a password login of a user with the second factor
    /// on, and every login of an admin with it on. Null: the session may start now.
    /// </summary>
    public async Task<string?> TicketIfNeededAsync(long userId, string method, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await IsOnAsync(db, userId, cancellationToken)) return null;
        if (method != SessionTokens.ByPassword && !(await users.GetAccessAsync(userId, cancellationToken)).IsAdmin) return null;

        var ticket = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var hash = TicketHash(ticket);
        var now = time.GetUtcNow();
        var expires = now.AddMinutes(options.CurrentValue.TotpTicketMinutes);
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM identity.totp_tickets WHERE expires_at <= {now};
            INSERT INTO identity.totp_tickets (ticket_hash, user_id, method, expires_at, tries) VALUES ({hash}, {userId}, {method}, {expires}, 0);
            """, cancellationToken);
        return ticket;
    }

    /// <summary>Counts one try of a ticket. Null: no such ticket, it ended, or it had all its tries.</summary>
    public async Task<(long UserId, string Method, bool LastTry)?> TryTicketAsync(string ticket, CancellationToken cancellationToken)
    {
        var hash = TicketHash(ticket);
        var now = time.GetUtcNow();
        var max = options.CurrentValue.TotpMaxTries;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Database.SqlQuery<string>($"""
            UPDATE identity.totp_tickets SET tries = tries + 1
            WHERE ticket_hash = {hash} AND expires_at > {now} AND tries < {max}
            RETURNING user_id::text || ' ' || tries::text || ' ' || method AS "Value"
            """).ToListAsync(cancellationToken);
        if (rows.Count == 0) return null;
        var parts = rows[0].Split(' ', 3);
        var tries = int.Parse(parts[1], CultureInfo.InvariantCulture);
        return (long.Parse(parts[0], CultureInfo.InvariantCulture), parts[2], tries >= max);
    }

    public async Task DropTicketAsync(string ticket, CancellationToken cancellationToken)
    {
        var hash = TicketHash(ticket);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.ExecuteSqlAsync($"DELETE FROM identity.totp_tickets WHERE ticket_hash = {hash}", cancellationToken);
    }

    /// <summary>The step of a right code inside the window around now, or null.</summary>
    private long? MatchingStep(byte[]? secret, string code)
    {
        if (secret is null) return null;
        var settings = options.CurrentValue;
        var now = Totp.Step(time.GetUtcNow(), settings.TotpStepSeconds);
        var given = Encoding.ASCII.GetBytes(code.Trim());
        long? found = null;
        // Every step of the window is checked, so the answer time does not tell which one matched.
        for (var step = now - settings.TotpWindowSteps; step <= now + settings.TotpWindowSteps; step++)
        {
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Totp.Code(secret, step)), given)) found ??= step;
        }

        return found;
    }

    /// <summary>80 random bits as "ABCD-EFGH-IJKL-MNOP": long and random, so a fast hash is enough to keep it.</summary>
    private static string NewRecoveryCode()
    {
        var text = Totp.ToBase32(RandomNumberGenerator.GetBytes(10));
        return $"{text[..4]}-{text[4..8]}-{text[8..12]}-{text[12..]}";
    }

    /// <summary>The hash of a recovery code, without regard to case, spaces and dashes.</summary>
    private static byte[] RecoveryHash(string code) => SHA256.HashData(Encoding.UTF8.GetBytes(
        code.Replace("-", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant()));

    private static byte[] TicketHash(string ticket) => SHA256.HashData(Encoding.UTF8.GetBytes(ticket));

    /// <summary>AES-GCM. The row keeps the nonce, the encrypted secret and the tag.</summary>
    private byte[] Seal(byte[] secret)
    {
        var result = new byte[NonceBytes + secret.Length + TagBytes];
        var nonce = result.AsSpan(0, NonceBytes);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(key.Derive("totp"), TagBytes);
        aes.Encrypt(nonce, secret, result.AsSpan(NonceBytes, secret.Length), result.AsSpan(NonceBytes + secret.Length));
        return result;
    }

    /// <summary>Null when the secret does not open (Identity:SigningKey changed): then only a recovery code works.</summary>
    private byte[]? Open(byte[] stored)
    {
        var secret = new byte[stored.Length - NonceBytes - TagBytes];
        using var aes = new AesGcm(key.Derive("totp"), TagBytes);
        try
        {
            aes.Decrypt(stored.AsSpan(0, NonceBytes), stored.AsSpan(NonceBytes, secret.Length), stored.AsSpan(NonceBytes + secret.Length), secret);
            return secret;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
