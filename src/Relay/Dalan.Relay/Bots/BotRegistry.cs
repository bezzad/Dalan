using System.Security.Cryptography;
using Dalan.Relay.Contracts;
using Dalan.Relay.Data;
using Microsoft.EntityFrameworkCore;

namespace Dalan.Relay.Bots;

public sealed class BotRegistry(IDbContextFactory<RelayDb> dbFactory, TimeProvider time)
{
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
}
