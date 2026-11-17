using System.Globalization;
using System.Net;
using Dalan.Admin.Contracts;
using Dalan.Admin.Data;
using Dalan.Admin.Settings;
using Dalan.Identity.Contracts;
using Dalan.Relay.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dalan.Admin.AdminApi;

internal sealed record SettingValueRequest(string? Value);

internal sealed record BuilderRequest(bool? Approve);

internal sealed record SuspendRequest(bool? Suspended);

internal sealed record SettingsResponse(IEnumerable<SettingEntry> Settings);

internal sealed record AccessRowJson(long Id, string Actor, long? ActorId, string? Phone, string Address, int? Port, DateTimeOffset At);

internal sealed record AuditRowJson(long Id, long UserId, string Action, string Target, string? Detail, DateTimeOffset At);

internal sealed record RowsResponse<T>(IEnumerable<T> Rows);

internal sealed record UsersResponse(IEnumerable<AdminUser> Users);

internal sealed record BotsResponse(IEnumerable<AdminBot> Bots);

internal sealed record ShowcaseResponse(IEnumerable<ShowcaseRequest> Entries);

/// <summary>ADM-05 to ADM-13: the one admin door. Identity and Relay keep their data; Admin calls their contracts.</summary>
internal static class AdminEndpoints
{
    public static void MapAdminApi(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin").RequireAuthorization().AddEndpointFilter<AdminFilter>();
        admin.MapGet("/settings", (SettingsCatalog catalog) => AdminJson.Ok(new SettingsResponse(catalog.List())));
        admin.MapPut("/settings/{key}", SetSettingAsync);
        admin.MapDelete("/settings/{key}", ResetSettingAsync);
        admin.MapGet("/audit", AuditAsync);
        admin.MapGet("/access-log", AccessLogAsync);
        admin.MapGet("/users", UsersAsync);
        admin.MapPost("/users/{userId:long}/builder", SetBuilderAsync);
        admin.MapPost("/users/{userId:long}/suspend", SuspendUserAsync);
        admin.MapGet("/bots", BotsAsync);
        admin.MapPost("/bots/{botId:long}/suspend", SuspendBotAsync);
        admin.MapGet("/showcase", ShowcaseAsync);
        admin.MapPost("/showcase/{botId:long}", ReviewShowcaseAsync);
    }

    private static readonly string[] AccessFilters = ["user", "bot", "phone", "address"];

    private static IResult SettingNotFound() => AdminJson.NotFound("setting_not_found",
        "There is no editable setting with this key. GET /api/admin/settings lists them.");

    private static async Task<IResult> SetSettingAsync(string key, HttpContext http, SettingsService settings)
    {
        if (await AdminJson.ReadAsync<SettingValueRequest>(http) is not { Value: { } value })
            return AdminJson.BadRequest("""Send JSON like {"value": "4096"}.""");

        var (outcome, entry, rule) = await settings.SetAsync(AdminFilter.UserId(http), key, value, http.RequestAborted);
        return outcome switch
        {
            SettingsService.Outcome.Done => AdminJson.Ok(entry),
            SettingsService.Outcome.BadValue => AdminJson.Error(StatusCodes.Status400BadRequest, "bad_value",
                $"{entry!.Key} must be {rule}."),
            _ => SettingNotFound(),
        };
    }

    private static async Task<IResult> ResetSettingAsync(string key, HttpContext http, SettingsService settings) =>
        await settings.ResetAsync(AdminFilter.UserId(http), key, http.RequestAborted) is { } entry
            ? AdminJson.Ok(entry)
            : SettingNotFound();

    /// <summary>ADM-09: newest first; "before" gives the next page.</summary>
    private static async Task<IResult> AuditAsync(
        HttpContext http, IDbContextFactory<AdminDb> dbFactory, IOptionsMonitor<AdminOptions> options)
    {
        if (!TryNumber(http.Request.Query["before"], out var before)) return AdminJson.BadRequest("\"before\" must be a row id.");

        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var rows = await db.Audit.AsNoTracking()
            .Where(a => before == null || a.Id < before)
            .OrderByDescending(a => a.Id)
            .Take(options.CurrentValue.PageSize)
            .Select(a => new AuditRowJson(a.Id, a.UserId, a.Action, a.Target, a.Detail, a.At))
            .ToListAsync(http.RequestAborted);
        return AdminJson.Ok(new RowsResponse<AuditRowJson>(rows));
    }

    /// <summary>ADM-10: exactly one of user, bot, phone or address; newest first.</summary>
    private static async Task<IResult> AccessLogAsync(
        HttpContext http, IDbContextFactory<AdminDb> dbFactory, IUserAdmin users, IOptionsMonitor<AdminOptions> options)
    {
        var query = http.Request.Query;
        var filters = AccessFilters.Where(query.ContainsKey).ToList();
        const string Help = "Send exactly one of user=<id>, bot=<id>, phone=<number> or address=<ip>, and optionally before=<row id>.";
        if (filters.Count != 1 || !TryNumber(query["before"], out var before)) return AdminJson.BadRequest(Help);

        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var rows = db.AccessLog.AsNoTracking().Where(e => before == null || e.Id < before);
        var text = query[filters[0]].ToString();
        switch (filters[0])
        {
            case "user" or "bot":
                if (!TryNumber(text, out var id) || id is null) return AdminJson.BadRequest(Help);
                var actor = filters[0] == "user" ? AccessActor.User : AccessActor.Bot;
                rows = rows.Where(e => e.Actor == actor && e.ActorId == id);
                break;
            case "phone":
                if (users.NormalizePhone(text) is not { } phone) return AdminJson.BadRequest(Help);
                rows = rows.Where(e => e.Phone == phone);
                break;
            default:
                if (!IPAddress.TryParse(text, out var address)) return AdminJson.BadRequest(Help);
                if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
                rows = rows.Where(e => e.Address == address);
                break;
        }

        var page = await rows.OrderByDescending(e => e.Id).Take(options.CurrentValue.PageSize).ToListAsync(http.RequestAborted);
#pragma warning disable CA1308 // Lower case names in JSON, like every other enum value of the API.
        return AdminJson.Ok(new RowsResponse<AccessRowJson>(page.Select(e => new AccessRowJson(
            e.Id, e.Actor.ToString().ToLowerInvariant(), e.Actor == AccessActor.Phone ? null : e.ActorId, e.Phone,
            e.Address.ToString(), e.Port, e.At))));
#pragma warning restore CA1308
    }

    /// <summary>ADM-11.</summary>
    private static async Task<IResult> UsersAsync(HttpContext http, IUserAdmin users, IOptionsMonitor<AdminOptions> options)
    {
        var limit = options.CurrentValue.PageSize;
        var query = http.Request.Query;
        if (query["builder"] == "requested")
            return AdminJson.Ok(new UsersResponse(await users.BuilderRequestsAsync(limit, http.RequestAborted)));

        return AdminJson.Ok(new UsersResponse(await users.SearchAsync(query["q"].ToString(), limit, http.RequestAborted)));
    }

    private static async Task<IResult> SetBuilderAsync(long userId, HttpContext http, IUserAdmin users, IAuditLog audit)
    {
        if (await AdminJson.ReadAsync<BuilderRequest>(http) is not { Approve: { } approve })
            return AdminJson.BadRequest("""Send JSON like {"approve": true}.""");

        if (await users.SetBuilderAsync(userId, approve, http.RequestAborted) is not { } user) return UserNotFound();
        await audit.RecordAsync(AdminFilter.UserId(http), approve ? "builder.approve" : "builder.refuse", UserTarget(userId), null,
            http.RequestAborted);
        return AdminJson.Ok(user);
    }

    /// <summary>AUTH-20: Identity ends the sessions, Relay ends the live streams.</summary>
    private static async Task<IResult> SuspendUserAsync(
        long userId, HttpContext http, IUserAdmin users, IBotAdmin bots, IAuditLog audit)
    {
        if (await AdminJson.ReadAsync<SuspendRequest>(http) is not { Suspended: { } suspended })
            return AdminJson.BadRequest("""Send JSON like {"suspended": true}.""");
        var adminId = AdminFilter.UserId(http);
        if (adminId == userId) return AdminJson.BadRequest("An admin cannot suspend their own account.");

        if (await users.SetSuspendedAsync(userId, suspended, http.RequestAborted) is not { } user) return UserNotFound();
        if (suspended) bots.EndUserStreams(userId);
        await audit.RecordAsync(adminId, suspended ? "user.suspend" : "user.unsuspend", UserTarget(userId), null, http.RequestAborted);
        return AdminJson.Ok(user);
    }

    /// <summary>ADM-12.</summary>
    private static async Task<IResult> BotsAsync(HttpContext http, IBotAdmin bots, IOptionsMonitor<AdminOptions> options) =>
        AdminJson.Ok(new BotsResponse(
            await bots.SearchAsync(http.Request.Query["q"].ToString(), options.CurrentValue.PageSize, http.RequestAborted)));

    private static async Task<IResult> SuspendBotAsync(long botId, HttpContext http, IBotAdmin bots, IAuditLog audit)
    {
        if (await AdminJson.ReadAsync<SuspendRequest>(http) is not { Suspended: { } suspended })
            return AdminJson.BadRequest("""Send JSON like {"suspended": true}.""");

        if (await bots.SetSuspendedAsync(botId, suspended, http.RequestAborted) is not { } bot)
            return AdminJson.NotFound("bot_not_found", "There is no bot with this id.");
        await audit.RecordAsync(AdminFilter.UserId(http), suspended ? "bot.suspend" : "bot.unsuspend",
            string.Create(CultureInfo.InvariantCulture, $"bot {botId}"), null, http.RequestAborted);
        return AdminJson.Ok(bot);
    }

    /// <summary>ADM-13.</summary>
    private static async Task<IResult> ShowcaseAsync(HttpContext http, IBotAdmin bots, IOptionsMonitor<AdminOptions> options) =>
        AdminJson.Ok(new ShowcaseResponse(await bots.ShowcaseRequestsAsync(options.CurrentValue.PageSize, http.RequestAborted)));

    private static async Task<IResult> ReviewShowcaseAsync(long botId, HttpContext http, IBotAdmin bots, IAuditLog audit)
    {
        if (await AdminJson.ReadAsync<BuilderRequest>(http) is not { Approve: { } approve })
            return AdminJson.BadRequest("""Send JSON like {"approve": true}.""");

        if (!await bots.ReviewShowcaseAsync(botId, approve, http.RequestAborted))
            return AdminJson.NotFound("showcase_not_found", "This bot has no showcase entry.");
        await audit.RecordAsync(AdminFilter.UserId(http), approve ? "showcase.approve" : "showcase.refuse",
            string.Create(CultureInfo.InvariantCulture, $"bot {botId}"), null, http.RequestAborted);
        return Results.NoContent();
    }

    private static IResult UserNotFound() => AdminJson.NotFound("user_not_found", "There is no user with this id.");

    private static string UserTarget(long userId) => string.Create(CultureInfo.InvariantCulture, $"user {userId}");

    /// <summary>True for no value (null) or a whole number of 0 or more.</summary>
    private static bool TryNumber(string? text, out long? value)
    {
        value = null;
        if (string.IsNullOrEmpty(text)) return true;
        if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number)) return false;
        value = number;
        return true;
    }
}
