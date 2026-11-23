using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Dalan.Relay.Contracts;
using Dalan.Relay.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dalan.Relay.Bots;

public sealed partial class BotRegistry(IDbContextFactory<RelayDb> dbFactory, IOptionsMonitor<RelayOptions> options, TimeProvider time)
{
    [GeneratedRegex("^[a-z0-9_]{1,32}$", RegexOptions.CultureInvariant)]
    private static partial Regex CommandName();

    /// <summary>Creates a bot and returns its token. This is the only moment the token exists in clear text.</summary>
    public async Task<(Bot Bot, string Token)> CreateAsync(
        long ownerUserId, string username, string name, CancellationToken cancellationToken)
    {
        var secret = BotToken.NewSecret();
        var bot = new Bot
        {
            Username = username,
            Name = name,
            OwnerUserId = ownerUserId,
            TokenHash = BotToken.Hash(secret),
            CreatedAt = time.GetUtcNow(),
        };

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.Bots.Add(bot);
        await db.SaveChangesAsync(cancellationToken);
        return (bot, BotToken.Format(bot.Id, secret));
    }

    /// <summary>Returns the bot when the token is valid, otherwise null.</summary>
    public async Task<(BotInfo Bot, bool Suspended)?> AuthenticateAsync(string? token, CancellationToken cancellationToken)
    {
        if (!BotToken.TryParse(token, out var botId, out var secret)) return null;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // No icon bytes: this runs on every bot call.
        var bot = await db.Bots.AsNoTracking().Where(b => b.Id == botId)
            .Select(b => new { b.Id, b.Username, b.Name, b.TokenHash, b.SuspendedAt })
            .FirstOrDefaultAsync(cancellationToken);

        return bot is not null && CryptographicOperations.FixedTimeEquals(bot.TokenHash, BotToken.Hash(secret))
            ? (new BotInfo(bot.Id, bot.Username, bot.Name), bot.SuspendedAt is not null)
            : null;
    }

    /// <summary>REL-37: a new token for the bot; the old one stops at once. Null when the bot does not exist.</summary>
    public async Task<string?> RotateTokenAsync(long botId, CancellationToken cancellationToken)
    {
        var secret = BotToken.NewSecret();
        var hash = BotToken.Hash(secret);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var changed = await db.Bots.Where(b => b.Id == botId)
            .ExecuteUpdateAsync(set => set.SetProperty(b => b.TokenHash, hash), cancellationToken);
        return changed == 0 ? null : BotToken.Format(botId, secret);
    }

    /// <summary>REL-26: replaces the command list. False when a command breaks a rule (nothing changes then).</summary>
    public async Task<bool> SetCommandsAsync(long botId, IReadOnlyList<BotCommand> commands, CancellationToken cancellationToken)
    {
        var limits = options.CurrentValue;
        var valid = commands.Count <= limits.MaxCommands && commands.All(c =>
            c is not null && CommandName().IsMatch(c.Command ?? "")
            && c.Description is { Length: > 0 } && c.Description.Length <= limits.MaxCommandDescriptionLength);
        if (!valid) return false;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var bot = await db.Bots.FirstAsync(b => b.Id == botId, cancellationToken);
        bot.Commands = [.. commands.Select(c => new CommandEntry { Command = c.Command, Description = c.Description })];
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<BotCommand>> GetCommandsAsync(long botId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var commands = await db.Bots.AsNoTracking().Where(b => b.Id == botId).Select(b => b.Commands).FirstOrDefaultAsync(cancellationToken);
        return commands?.ConvertAll(c => new BotCommand(c.Command, c.Description)) ?? [];
    }
}
