using Dalan.Relay.Bots;
using Dalan.Relay.Chats;
using Microsoft.EntityFrameworkCore;

namespace Dalan.Relay.Data;

public sealed class RelayDb(DbContextOptions<RelayDb> options) : DbContext(options)
{
    public const string Schema = "relay";

    public DbSet<Bot> Bots => Set<Bot>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        // Bot username is unique without regard to case: see the index on lower(username) in the first migration.

        modelBuilder.Entity<Membership>(membership =>
        {
            membership.HasKey(m => new { m.BotId, m.UserId });
            membership.HasOne<Bot>().WithMany().HasForeignKey(m => m.BotId);
        });

        modelBuilder.Entity<Message>(message =>
        {
            message.OwnsOne(m => m.Content, content => content.ToJson());
            message.HasOne<Membership>().WithMany().HasForeignKey(m => new { m.BotId, m.UserId });
            message.HasIndex(m => new { m.BotId, m.UserId, m.Seq }).IsUnique();
            // Small index for the reload on start (Q-07): only messages that still wait for a bot.
            message.HasIndex(m => m.Id).HasFilter("delivery = 0").HasDatabaseName("ix_messages_pending");
        });
    }

    /// <summary>Writes the outcome of user messages. Each message leaves the queue once, so each id is written once.</summary>
    public static async Task MarkAsync(
        IDbContextFactory<RelayDb> dbFactory, IReadOnlyCollection<long> messageIds, DeliveryState state)
    {
        if (messageIds.Count == 0) return;

        await using var db = await dbFactory.CreateDbContextAsync();
        await db.Messages
            .Where(m => messageIds.Contains(m.Id))
            .ExecuteUpdateAsync(set => set.SetProperty(m => m.Delivery, state));
    }
}
