using System.Text.RegularExpressions;
using Dalan.Admin.Contracts;
using Dalan.Identity.Contracts;
using Dalan.Relay.Bots;
using Dalan.Relay.Data;
using Dalan.Relay.Showcase;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Dalan.Relay.Builder;

internal sealed record CreateBotRequest(string? Username, string? Name);

internal sealed record BuilderBotJson(
    long Id, string Username, string Name, DateTimeOffset CreatedAt, bool Online, DateTimeOffset? LastSeen, bool Suspended,
    ShowcaseJson? Showcase);

internal sealed record BuilderBotsResponse(IEnumerable<BuilderBotJson> Bots);

internal sealed record NewTokenJson(long Id, string Username, string Name, string Token);

/// <summary>What a builder call answers: the HTTP status and the JSON body (an <see cref="ErrorJson"/> when refused).</summary>
internal sealed record BuilderAnswer(int Status, object Body)
{
    public static BuilderAnswer Error(int status, string code, string message) => new(status, new ErrorJson(code, message));
}

/// <summary>
/// REL-36, REL-37, REL-39, REL-45: a builder's own bots. The one place of these rules; the REST calls and the
/// MCP tools (REL-46) only turn the answer into HTTP or MCP.
/// </summary>
internal sealed partial class BuilderBots(
    IDbContextFactory<RelayDb> dbFactory,
    BotRegistry bots,
    BotPresence presence,
    IAuditLog audit,
    IUserDirectory users,
    IOptionsMonitor<RelayOptions> options)
{
    public async Task<BuilderAnswer> ListAsync(long userId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return new BuilderAnswer(StatusCodes.Status200OK,
            new BuilderBotsResponse(await ReadAsync(db, db.Bots.Where(b => b.OwnerUserId == userId), cancellationToken)));
    }

    /// <summary>REL-45: one bot with its status, for its owner or an admin.</summary>
    public async Task<BuilderAnswer> GetAsync(long userId, long botId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await MayChangeAsync(db, userId, botId, cancellationToken)) return BotNotFound();
        var found = await ReadAsync(db, db.Bots.Where(b => b.Id == botId), cancellationToken);
        return new BuilderAnswer(StatusCodes.Status200OK, found[0]);
    }

    public async Task<BuilderAnswer> CreateAsync(long userId, CreateBotRequest? request, CancellationToken cancellationToken)
    {
        var limits = options.CurrentValue;
#pragma warning disable CA1308 // Bot usernames are stored in lower case.
        var username = request?.Username?.Trim().TrimStart('@').ToLowerInvariant() ?? "";
#pragma warning restore CA1308
        var name = request?.Name?.Trim() ?? "";
        if (!UsernameShape().IsMatch(username) || name.Length == 0 || name.Length > limits.MaxBotNameLength)
        {
            return BuilderAnswer.Error(StatusCodes.Status400BadRequest, "bad_request",
                $$"""Send JSON like {"username": "weather_bot", "name": "Weather"}: a username of 5 to 32 characters of a-z, 0-9 and _ that starts with a letter, and a name of 1 to {{limits.MaxBotNameLength}} characters.""");
        }

        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            if (await db.Bots.CountAsync(b => b.OwnerUserId == userId, cancellationToken) >= limits.MaxBotsPerBuilder)
            {
                return BuilderAnswer.Error(StatusCodes.Status409Conflict, "bot_limit",
                    $"You already have {limits.MaxBotsPerBuilder} bots, the most one builder may have.");
            }
        }

        try
        {
            var (bot, token) = await bots.CreateAsync(userId, username, name, cancellationToken);
            await audit.RecordAsync(userId, "bot.create", BuilderEndpoints.BotTarget(bot.Id), bot.Username, cancellationToken);
            return new BuilderAnswer(StatusCodes.Status201Created, new NewTokenJson(bot.Id, bot.Username, bot.Name, token));
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return BuilderAnswer.Error(StatusCodes.Status409Conflict, "username_taken", "Another bot has this username. Choose another one.");
        }
    }

    public async Task<BuilderAnswer> RotateAsync(long userId, long botId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var bot = await db.Bots.AsNoTracking().FirstOrDefaultAsync(b => b.Id == botId, cancellationToken);
        if (!await MayChangeAsync(db, userId, botId, cancellationToken) || await bots.RotateTokenAsync(botId, cancellationToken) is not { } token)
            return BotNotFound();

        await audit.RecordAsync(userId, "bot.token", BuilderEndpoints.BotTarget(botId), null, cancellationToken);
        return new BuilderAnswer(StatusCodes.Status200OK, new NewTokenJson(bot!.Id, bot.Username, bot.Name, token));
    }

    /// <summary>REL-37 and REL-40: only the owner of the bot, or an admin.</summary>
    public async Task<bool> MayChangeAsync(RelayDb db, long userId, long botId, CancellationToken cancellationToken)
    {
        var ownerId = await db.Bots.Where(b => b.Id == botId).Select(b => (long?)b.OwnerUserId).FirstOrDefaultAsync(cancellationToken);
        return ownerId is not null && (ownerId == userId || (await users.GetAccessAsync(userId, cancellationToken)).IsAdmin);
    }

    private async Task<List<BuilderBotJson>> ReadAsync(RelayDb db, IQueryable<Bot> query, CancellationToken cancellationToken)
    {
        var found = await query.AsNoTracking()
            .OrderBy(b => b.Id)
            .Select(b => new { b.Id, b.Username, b.Name, b.CreatedAt, b.LastSeenAt, Suspended = b.SuspendedAt != null })
            .ToListAsync(cancellationToken);
        var ids = found.ConvertAll(b => b.Id);
        // REL-40: the entry without the icon bytes; "icon" only says whether there is one.
        var entries = await db.Showcase.AsNoTracking().Where(e => ids.Contains(e.BotId))
            .Select(e => new { e.BotId, e.State, e.Description, e.Category, Icon = e.Icon != null })
            .ToDictionaryAsync(e => e.BotId, e => ShowcaseJson.Of(e.State, e.Description, e.Category, e.Icon), cancellationToken);
        return found.ConvertAll(b =>
        {
            var status = presence.Of(b.Id, b.LastSeenAt);
            return new BuilderBotJson(b.Id, b.Username, b.Name, b.CreatedAt, status.Online, status.LastSeen, b.Suspended,
                entries.GetValueOrDefault(b.Id));
        });
    }

    private static BuilderAnswer BotNotFound() =>
        BuilderAnswer.Error(StatusCodes.Status404NotFound, "bot_not_found", "You have no bot with this id.");

    [GeneratedRegex("^[a-z][a-z0-9_]{4,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernameShape();
}
