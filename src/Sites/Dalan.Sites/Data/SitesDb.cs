using Dalan.Sites.Demos;
using Dalan.Sites.Names;
using Microsoft.EntityFrameworkCore;

namespace Dalan.Sites.Data;

public sealed class SitesDb(DbContextOptions<SitesDb> options) : DbContext(options)
{
    public const string Schema = "sites";

    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Demo> Demos => Set<Demo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Site>(site =>
        {
            site.ToTable("sites");
            site.HasIndex(s => s.Name).IsUnique();
            site.HasIndex(s => s.OwnerUserId);
        });
        modelBuilder.Entity<Demo>(demo =>
        {
            demo.ToTable("demos");
            demo.HasKey(d => d.Id);
            demo.HasIndex(d => d.OwnerUserId);
        });
    }
}
