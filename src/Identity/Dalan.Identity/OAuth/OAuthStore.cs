using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Dalan.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dalan.Identity.OAuth;

internal sealed record ClientRow(string ClientId, long OwnerUserId, byte[] SecretHash, DateTimeOffset CreatedAt);

internal sealed record ConsentRow(string ClientId, long OwnerUserId, string Scopes, bool Email, DateTimeOffset CreatedAt);

internal sealed record CodeRow(
    string ClientId, long UserId, string RedirectUri, string Challenge, string? Nonce, string Scopes, bool Email, bool Proxy,
    DateTimeOffset ExpiresAt, DateTimeOffset? UsedAt);

internal sealed record TokenRow(string ClientId, long UserId, string Scopes, bool Email);

/// <summary>AUTH-40 to AUTH-43: the clients, consents, codes and access tokens. Secrets, codes and tokens are kept only as hashes.</summary>
internal sealed class OAuthStore(IDbContextFactory<IdentityDb> dbFactory, IOptionsMonitor<IdentityOptions> options, TimeProvider time)
{
    /// <summary>256 random bits: long and random, so SHA-256 is enough to store it (like a bot token).</summary>
    public static string NewSecret() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public static byte[] Hash(string secret) => SHA256.HashData(Encoding.UTF8.GetBytes(secret));

    public async Task<ClientRow?> FindClientAsync(string clientId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Database.SqlQuery<ClientRow>($"""
            SELECT client_id, owner_user_id, secret_hash, created_at FROM identity.oauth_clients WHERE client_id = {clientId}
            """).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<ClientRow>> ClientsOfAsync(long ownerUserId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Database.SqlQuery<ClientRow>($"""
            SELECT client_id, owner_user_id, secret_hash, created_at FROM identity.oauth_clients
            WHERE owner_user_id = {ownerUserId} ORDER BY client_id
            """).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Turns the client on. Null when it was already on for this owner; else the new secret. A row of an earlier owner of the
    /// name (the site was deleted and taken again) is replaced.
    /// </summary>
    public async Task<string?> TurnOnAsync(string clientId, long ownerUserId, CancellationToken cancellationToken)
    {
        var secret = NewSecret();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var changed = await db.Database.ExecuteSqlAsync($"""
            INSERT INTO identity.oauth_clients (client_id, owner_user_id, secret_hash, created_at)
            VALUES ({clientId}, {ownerUserId}, {Hash(secret)}, {time.GetUtcNow()})
            ON CONFLICT (client_id) DO UPDATE SET owner_user_id = EXCLUDED.owner_user_id, secret_hash = EXCLUDED.secret_hash,
                created_at = EXCLUDED.created_at
            WHERE identity.oauth_clients.owner_user_id <> EXCLUDED.owner_user_id
            """, cancellationToken);
        return changed == 0 ? null : secret;
    }

    /// <summary>A new secret in place of the old one; null when the owner has no such client.</summary>
    public async Task<string?> NewSecretAsync(string clientId, long ownerUserId, CancellationToken cancellationToken)
    {
        var secret = NewSecret();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var changed = await db.Database.ExecuteSqlAsync($"""
            UPDATE identity.oauth_clients SET secret_hash = {Hash(secret)}
            WHERE client_id = {clientId} AND owner_user_id = {ownerUserId}
            """, cancellationToken);
        return changed == 0 ? null : secret;
    }

    public async Task<bool> TurnOffAsync(string clientId, long ownerUserId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Database.ExecuteSqlAsync(
            $"DELETE FROM identity.oauth_clients WHERE client_id = {clientId} AND owner_user_id = {ownerUserId}", cancellationToken) > 0;
    }

    public async Task<ConsentRow?> FindConsentAsync(long userId, string clientId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Database.SqlQuery<ConsentRow>($"""
            SELECT client_id, owner_user_id, scopes, email, created_at FROM identity.oauth_consents
            WHERE user_id = {userId} AND client_id = {clientId}
            """).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<ConsentRow>> ConsentsOfAsync(long userId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Database.SqlQuery<ConsentRow>($"""
            SELECT client_id, owner_user_id, scopes, email, created_at FROM identity.oauth_consents
            WHERE user_id = {userId} ORDER BY created_at
            """).ToListAsync(cancellationToken);
    }

    public async Task SaveConsentAsync(long userId, string clientId, long ownerUserId, string scopes, bool email, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO identity.oauth_consents (user_id, client_id, owner_user_id, scopes, email, created_at)
            VALUES ({userId}, {clientId}, {ownerUserId}, {scopes}, {email}, {time.GetUtcNow()})
            ON CONFLICT (user_id, client_id) DO UPDATE SET owner_user_id = EXCLUDED.owner_user_id, scopes = EXCLUDED.scopes,
                email = EXCLUDED.email, created_at = EXCLUDED.created_at
            """, cancellationToken);
    }

    public async Task<bool> RemoveConsentAsync(long userId, string clientId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Database.ExecuteSqlAsync(
            $"DELETE FROM identity.oauth_consents WHERE user_id = {userId} AND client_id = {clientId}", cancellationToken) > 0;
    }

    /// <summary>AUTH-41: a new one-use code. Old codes go at the same time.</summary>
    public async Task<string> NewCodeAsync(
        string clientId, long userId, string redirectUri, string challenge, string? nonce, string scopes, bool email, bool proxy,
        CancellationToken cancellationToken)
    {
        var code = NewSecret();
        var now = time.GetUtcNow();
        var expires = now.AddSeconds(options.CurrentValue.OAuthCodeSeconds);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.ExecuteSqlAsync($"DELETE FROM identity.oauth_codes WHERE expires_at <= {now}", cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO identity.oauth_codes (code_hash, client_id, user_id, redirect_uri, challenge, nonce, scopes, email, proxy, expires_at)
            VALUES ({Hash(code)}, {clientId}, {userId}, {redirectUri}, {challenge}, {nonce}, {scopes}, {email}, {proxy}, {expires})
            """, cancellationToken);
        return code;
    }

    public async Task<CodeRow?> FindCodeAsync(string code, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Database.SqlQuery<CodeRow>($"""
            SELECT client_id, user_id, redirect_uri, challenge, nonce, scopes, email, proxy, expires_at, used_at
            FROM identity.oauth_codes WHERE code_hash = {Hash(code)}
            """).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Spends the code: true only for the one call that finds it unused and in time. A code that comes again ends the tokens
    /// it gave (RFC 6749 section 4.1.2: it may have been stolen).
    /// </summary>
    public async Task<bool> SpendCodeAsync(string code, CancellationToken cancellationToken)
    {
        var hash = Hash(code);
        var now = time.GetUtcNow();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var spent = await db.Database.ExecuteSqlAsync($"""
            UPDATE identity.oauth_codes SET used_at = {now} WHERE code_hash = {hash} AND used_at IS NULL AND expires_at > {now}
            """, cancellationToken);
        if (spent == 0) await db.Database.ExecuteSqlAsync($"DELETE FROM identity.oauth_tokens WHERE code_hash = {hash}", cancellationToken);
        return spent == 1;
    }

    /// <summary>AUTH-42: a new access token for /oauth/userinfo. Old tokens go at the same time.</summary>
    public async Task<string> NewTokenAsync(string code, CodeRow row, CancellationToken cancellationToken)
    {
        var token = NewSecret();
        var now = time.GetUtcNow();
        var expires = now.AddMinutes(options.CurrentValue.OAuthTokenMinutes);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.ExecuteSqlAsync($"DELETE FROM identity.oauth_tokens WHERE expires_at <= {now}", cancellationToken);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO identity.oauth_tokens (token_hash, code_hash, client_id, user_id, scopes, email, expires_at)
            VALUES ({Hash(token)}, {Hash(code)}, {row.ClientId}, {row.UserId}, {row.Scopes}, {row.Email}, {expires})
            """, cancellationToken);
        return token;
    }

    public async Task<TokenRow?> FindTokenAsync(string token, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Database.SqlQuery<TokenRow>($"""
            SELECT client_id, user_id, scopes, email FROM identity.oauth_tokens
            WHERE token_hash = {Hash(token)} AND expires_at > {time.GetUtcNow()}
            """).FirstOrDefaultAsync(cancellationToken);
    }
}
