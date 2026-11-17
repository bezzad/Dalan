using Dalan.Notification.Push;
using Microsoft.EntityFrameworkCore;

namespace Dalan.Notification.Data;

public sealed class NotificationDb(DbContextOptions<NotificationDb> options) : DbContext(options)
{
    public const string Schema = "notification";

    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Subscription>(subscription =>
        {
            subscription.ToTable("push_subscriptions");
            // NTF-03: one owner per endpoint.
            subscription.HasIndex(s => s.Endpoint).IsUnique();
            subscription.HasIndex(s => new { s.UserId, s.CreatedAt });
        });
    }
}
