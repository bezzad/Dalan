using Dalan.Identity.Keys;
using Dalan.Identity.Sessions;
using Dalan.Identity.SmsLogin;
using Dalan.Identity.Users;
using Microsoft.EntityFrameworkCore;

namespace Dalan.Identity.Data;

public sealed class IdentityDb(DbContextOptions<IdentityDb> options) : DbContext(options)
{
    public const string Schema = "identity";

    public DbSet<User> Users => Set<User>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<SmsCode> SmsCodes => Set<SmsCode>();
    public DbSet<AccessKey> AccessKeys => Set<AccessKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<User>(user =>
        {
            user.HasIndex(u => u.Phone).IsUnique();
            // Username is unique without regard to case: see the index on lower(username) in the first migration.
        });
        modelBuilder.Entity<Session>(session =>
        {
            session.HasIndex(s => s.TokenHash).IsUnique();
            session.HasIndex(s => s.PreviousHash);
            // A deleted user has no sessions.
            session.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<AccessKey>(key =>
        {
            key.HasIndex(k => k.KeyHash).IsUnique();
            key.HasIndex(k => k.UserId);
            key.HasOne<User>().WithMany().HasForeignKey(k => k.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<SmsCode>(code =>
        {
            code.HasIndex(c => new { c.Phone, c.CreatedAt });
            code.HasIndex(c => c.CreatedAt);
        });
    }
}
