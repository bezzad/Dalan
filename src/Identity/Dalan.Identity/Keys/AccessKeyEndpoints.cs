using System.Globalization;
using Dalan.Admin.Contracts;
using Dalan.Identity.Data;
using Dalan.Identity.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dalan.Identity.Keys;

internal sealed record KeyRequest(string? Name);

internal sealed record AddressesRequest(string?[]? Addresses);

/// <summary>AUTH-23, AUTH-38: a key in the list. An empty <see cref="Addresses"/> means any address.</summary>
internal sealed record KeyJson(long Id, string Name, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, string[] Addresses);

internal sealed record NewKeyJson(long Id, string Name, string Key, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, string[] Addresses);

internal sealed record KeysResponse(IEnumerable<KeyJson> Keys);

/// <summary>
/// AUTH-21 and AUTH-23: a user makes, lists and deletes access keys. Only with a session (the default policy),
/// never with a key, so a stolen key cannot make more keys. Each call writes the access log (ADM-01).
/// </summary>
internal static class AccessKeyEndpoints
{
    public static void MapAccessKeys(this IEndpointRouteBuilder endpoints)
    {
        var keys = endpoints.MapGroup("/api/account/keys").RequireAuthorization().AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            await http.RequestServices.GetRequiredService<IAccessLog>().RecordAsync(AccessActor.User,
                SessionTokens.UserId(http.User), http.Connection.RemoteIpAddress, http.Connection.RemotePort, http.RequestAborted);
            return await next(context);
        });
        keys.MapGet("", ListAsync);
        keys.MapPost("", CreateAsync);
        keys.MapDelete("/{keyId:long}", DeleteAsync);
        keys.MapPut("/{keyId:long}/addresses", SetAddressesAsync);
    }

    private static async Task<IResult> ListAsync(HttpContext http, IDbContextFactory<IdentityDb> dbFactory)
    {
        var userId = SessionTokens.UserId(http.User);
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var keys = await db.AccessKeys.AsNoTracking().Where(k => k.UserId == userId).OrderBy(k => k.Id)
            .Select(k => new KeyJson(k.Id, k.Name, k.CreatedAt, k.ExpiresAt, k.Addresses))
            .ToListAsync(http.RequestAborted);
        return AuthJson.Ok(new KeysResponse(keys));
    }

    private static async Task<IResult> CreateAsync(
        HttpContext http, IDbContextFactory<IdentityDb> dbFactory, IAuditLog audit, TimeProvider time,
        IOptionsMonitor<IdentityOptions> options)
    {
        var settings = options.CurrentValue;
        var name = (await AuthJson.ReadAsync<KeyRequest>(http))?.Name?.Trim() ?? "";
        if (name.Length == 0 || name.Length > settings.MaxNameLength)
        {
            return AuthJson.BadRequest("bad_request",
                $$"""Send JSON like {"name": "laptop"}: a name of 1 to {{settings.MaxNameLength}} characters.""");
        }

        var userId = SessionTokens.UserId(http.User);
        var now = time.GetUtcNow();
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        // Expired keys are of no more use.
        await db.AccessKeys.Where(k => k.UserId == userId && k.ExpiresAt <= now).ExecuteDeleteAsync(http.RequestAborted);
        if (await db.AccessKeys.CountAsync(k => k.UserId == userId, http.RequestAborted) >= settings.MaxAccessKeys)
        {
            return AuthJson.Error(StatusCodes.Status409Conflict, "key_limit",
                $"You already have {settings.MaxAccessKeys} access keys, the most one user may have. Delete one first.");
        }

        var text = KeyText.New();
        var key = new AccessKey
        {
            UserId = userId,
            Name = name,
            KeyHash = KeyText.Hash(text),
            CreatedAt = now,
            ExpiresAt = now.AddDays(settings.AccessKeyDays),
        };
        db.AccessKeys.Add(key);
        await db.SaveChangesAsync(http.RequestAborted);
        await audit.RecordAsync(userId, "key.create", Target(key.Id), name, http.RequestAborted);
        return Results.Json(new NewKeyJson(key.Id, key.Name, text, key.CreatedAt, key.ExpiresAt, key.Addresses), AuthJson.Options,
            statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> DeleteAsync(long keyId, HttpContext http, IDbContextFactory<IdentityDb> dbFactory, IAuditLog audit)
    {
        var userId = SessionTokens.UserId(http.User);
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var deleted = await db.AccessKeys.Where(k => k.Id == keyId && k.UserId == userId).ExecuteDeleteAsync(http.RequestAborted);
        if (deleted == 0) return AuthJson.Error(StatusCodes.Status404NotFound, "key_not_found", "You have no access key with this id.");

        await audit.RecordAsync(userId, "key.delete", Target(keyId), null, http.RequestAborted);
        return Results.NoContent();
    }

    /// <summary>AUTH-38: replaces the address list of a key. An empty list: the key works from any address.</summary>
    private static async Task<IResult> SetAddressesAsync(
        long keyId, HttpContext http, IDbContextFactory<IdentityDb> dbFactory, IAuditLog audit, IOptionsMonitor<IdentityOptions> options)
    {
        var max = options.CurrentValue.MaxKeyAddresses;
        var given = (await AuthJson.ReadAsync<AddressesRequest>(http))?.Addresses;
        var addresses = given?.Select(KeyAddresses.Normalize).ToList();
        if (addresses is null || addresses.Contains(null))
        {
            return AuthJson.BadRequest("bad_address",
                """Send JSON like {"addresses": ["80.244.7.0/24", "2a01::1"]}: IPv4 or IPv6 addresses, or CIDR ranges that start at their first address.""");
        }

        string[] list = [.. addresses.OfType<string>().Distinct()];
        if (list.Length > max)
            return AuthJson.BadRequest("too_many_addresses", $"A key may have at most {max} addresses or ranges.");

        var userId = SessionTokens.UserId(http.User);
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var key = await db.AccessKeys.FirstOrDefaultAsync(k => k.Id == keyId && k.UserId == userId, http.RequestAborted);
        if (key is null) return AuthJson.Error(StatusCodes.Status404NotFound, "key_not_found", "You have no access key with this id.");

        key.Addresses = list;
        await db.SaveChangesAsync(http.RequestAborted);
        await audit.RecordAsync(userId, "key.addresses", Target(keyId), list.Length == 0 ? "any" : string.Join(' ', list), http.RequestAborted);
        return AuthJson.Ok(new KeyJson(key.Id, key.Name, key.CreatedAt, key.ExpiresAt, key.Addresses));
    }

    private static string Target(long keyId) => string.Create(CultureInfo.InvariantCulture, $"key {keyId}");
}
