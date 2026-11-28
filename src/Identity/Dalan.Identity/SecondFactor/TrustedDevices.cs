using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Dalan.Identity.Data;
using Dalan.Identity.Sessions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dalan.Identity.SecondFactor;

/// <summary>AUTH-44: one trusted browser in the list.</summary>
internal sealed record TrustedDeviceRow(long Id, string Browser, string Os, string? Address, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, byte[] TokenHash);

/// <summary>
/// AUTH-44: "Don't ask for the code on this device". A random token in an HttpOnly cookie; only its hash is kept, with the
/// user. A later login of the same user on that browser skips the second factor. Only raw SQL reads the table.
/// </summary>
internal sealed class TrustedDevices(IDbContextFactory<IdentityDb> dbFactory, IOptionsMonitor<IdentityOptions> options, TimeProvider time)
{
    /// <summary>"__Host-": the browser refuses it without Secure or with a Domain.</summary>
    public const string CookieName = "__Host-dalan_trusted";

    private int Days => options.CurrentValue.TrustedDeviceDays;

    /// <summary>True when this browser sent a live trusted token of this user, and trust is on.</summary>
    public async Task<bool> IsTrustedAsync(HttpContext http, long userId)
    {
        if (Days <= 0 || TokenHash(http) is not { } hash) return false;
        var now = time.GetUtcNow();
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        return (await db.Database.SqlQuery<bool>($"""
            SELECT EXISTS (SELECT 1 FROM identity.trusted_devices
                WHERE token_hash = {hash} AND user_id = {userId} AND expires_at > {now}) AS "Value"
            """).ToListAsync(http.RequestAborted))[0];
    }

    /// <summary>Trusts this browser for the user: a new token, its hash kept, the cookie set. Nothing when trust is off.</summary>
    public async Task TrustAsync(HttpContext http, long userId)
    {
        var days = Days;
        if (days <= 0) return;
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var hash = Hash(token);
        var replaced = TokenHash(http) ?? [];
        var now = time.GetUtcNow();
        var expires = now.AddDays(days);
        var (browser, os) = UserAgent.Parse(http.Request.Headers.UserAgent);
        var address = SessionTokens.Address(http);
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        // Rows of no more use: the user's ended ones, and the one this browser had until now.
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM identity.trusted_devices WHERE (user_id = {userId} AND expires_at <= {now}) OR token_hash = {replaced};
            INSERT INTO identity.trusted_devices (token_hash, user_id, browser, os, address, created_at, expires_at)
            VALUES ({hash}, {userId}, {browser}, {os}, {address}, {now}, {expires});
            """, http.RequestAborted);
        http.Response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            // Lax: the cookie also comes with the way back from GitHub. Alone it makes no session: the first step is still needed.
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = TimeSpan.FromDays(days),
        });
    }

    public async Task<List<TrustedDeviceRow>> ListAsync(long userId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Database.SqlQuery<TrustedDeviceRow>($"""
            SELECT id, browser, os, host(address) AS address, created_at, expires_at, token_hash
            FROM identity.trusted_devices WHERE user_id = {userId} AND expires_at > {now}
            ORDER BY created_at DESC, id DESC
            """).ToListAsync(cancellationToken);
    }

    /// <summary>Forgets one; false when the user has no such one.</summary>
    public async Task<bool> ForgetAsync(long userId, long id, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Database.ExecuteSqlAsync(
            $"DELETE FROM identity.trusted_devices WHERE id = {id} AND user_id = {userId}", cancellationToken) == 1;
    }

    /// <summary>Forgets every trusted browser of the user. Returns how many.</summary>
    public async Task<int> ForgetAllAsync(long userId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Database.ExecuteSqlAsync($"DELETE FROM identity.trusted_devices WHERE user_id = {userId}", cancellationToken);
    }

    /// <summary>Forgets every trusted browser of the user but this one (the password is set, the others are logged out).</summary>
    public static Task<int> ForgetOthersAsync(IdentityDb db, HttpContext http, long userId)
    {
        var current = TokenHash(http) ?? [];
        return db.Database.ExecuteSqlAsync(
            $"DELETE FROM identity.trusted_devices WHERE user_id = {userId} AND token_hash <> {current}", http.RequestAborted);
    }

    /// <summary>The hash of the token this browser sent, or null when it sent none.</summary>
    public static byte[]? TokenHash(HttpContext http) =>
        http.Request.Cookies[CookieName] is { Length: > 0 } token ? Hash(token) : null;

    /// <summary>256 random bits: a fast hash is enough to keep it (like a refresh token).</summary>
    private static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
}
