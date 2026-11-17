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
    public async Task<BotInfo?> AuthenticateAsync(string? token, CancellationToken cancellationToken)
    {
        if (!BotToken.TryParse(token, out var botId, out var secret)) return null;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var bot = await db.Bots.AsNoTracking().FirstOrDefaultAsync(b => b.Id == botId, cancellationToken);

        return bot is not null && CryptographicOperations.FixedTimeEquals(bot.TokenHash, BotToken.Hash(secret))
            ? new BotInfo(bot.Id, bot.Username, bot.Name)
            : null;
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
        var bot = await db.Bots.AsNoTracking().FirstOrDefaultAsync(b => b.Id == botId, cancellationToken);
        return bot?.Commands.ConvertAll(c => new BotCommand(c.Command, c.Description)) ?? [];
    }
}
