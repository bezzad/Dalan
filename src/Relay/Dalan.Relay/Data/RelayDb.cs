using Dalan.Relay.Bots;
using Dalan.Relay.Chats;
using Dalan.Relay.Files;
using Dalan.Relay.Outbox;
using Dalan.Relay.Showcase;
using Microsoft.EntityFrameworkCore;

namespace Dalan.Relay.Data;

public sealed class RelayDb(DbContextOptions<RelayDb> options) : DbContext(options)
{
    public const string Schema = "relay";

    public DbSet<Bot> Bots => Set<Bot>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<OutboxEvent> Outbox => Set<OutboxEvent>();
    public DbSet<StoredFile> Files => Set<StoredFile>();
    public DbSet<ShowcaseEntry> Showcase => Set<ShowcaseEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        // Bot username is unique without regard to case: see the index on lower(username) in the first migration.
        modelBuilder.Entity<Bot>().OwnsMany(b => b.Commands, commands => commands.ToJson());

        modelBuilder.Entity<Membership>(membership =>
        {
            membership.HasKey(m => new { m.BotId, m.UserId });
            membership.HasOne<Bot>().WithMany().HasForeignKey(m => m.BotId);
        });

        modelBuilder.Entity<Message>(message =>
        {
            message.OwnsOne(m => m.Content, content =>
            {
                content.ToJson();
                content.OwnsOne(c => c.ReplyKeyboard, keyboard => keyboard.OwnsMany(k => k.Rows));
                content.OwnsOne(c => c.InlineKeyboard, keyboard =>
                    keyboard.OwnsMany(k => k.Rows, row => row.OwnsMany(r => r.Buttons)));
                content.OwnsOne(c => c.File);
            });
            message.HasOne<Membership>().WithMany().HasForeignKey(m => new { m.BotId, m.UserId });
            message.HasIndex(m => new { m.BotId, m.UserId, m.Seq }).IsUnique();
            message.HasIndex(m => new { m.BotId, m.UserId, m.Version }).IsUnique();
            // Small index for the reload on start (Q-07): only messages that still wait for a bot.
            message.HasIndex(m => m.Id).HasFilter("delivery = 0").HasDatabaseName("ix_messages_pending");
        });

        modelBuilder.Entity<StoredFile>(file =>
        {
            file.HasIndex(f => f.ExpiresAt);
            file.HasIndex(f => new { f.UserId, f.CreatedAt });
        });

        modelBuilder.Entity<ShowcaseEntry>(entry =>
        {
            entry.ToTable("showcase");
            entry.HasKey(e => e.BotId);
            entry.HasOne<Bot>().WithOne().HasForeignKey<ShowcaseEntry>(e => e.BotId);
            entry.HasIndex(e => e.State);
        });

        modelBuilder.Entity<OutboxEvent>(outbox =>
        {
            outbox.ToTable("outbox");
            outbox.Property(e => e.Payload).HasColumnType("jsonb");
        });
    }
}
