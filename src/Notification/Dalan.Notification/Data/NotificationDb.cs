using Dalan.Notification.Inbox;
using Dalan.Notification.Push;
using Microsoft.EntityFrameworkCore;

namespace Dalan.Notification.Data;

public sealed class NotificationDb(DbContextOptions<NotificationDb> options) : DbContext(options)
{
    public const string Schema = "notification";

    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    public DbSet<Notice> Inbox => Set<Notice>();

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

        modelBuilder.Entity<Notice>(notice =>
        {
            notice.ToTable("inbox");
            // NTF-12: a user's page, newest first; NTF-13: the SMS of the last day.
            notice.HasIndex(n => new { n.UserId, n.Id });
            notice.HasIndex(n => n.CreatedAt);
        });
    }
}
