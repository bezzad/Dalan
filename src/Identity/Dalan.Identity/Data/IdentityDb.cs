using Dalan.Identity.Users;
using Microsoft.EntityFrameworkCore;

namespace Dalan.Identity.Data;

public sealed class IdentityDb(DbContextOptions<IdentityDb> options) : DbContext(options)
{
    public const string Schema = "identity";

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<User>(user =>
        {
            user.HasIndex(u => u.Phone).IsUnique();
            // Username is unique without regard to case: see the index on lower(username) in the first migration.
        });
    }
}
