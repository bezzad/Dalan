using Dalan.Admin.AccessLog;
using Dalan.Admin.Audit;
using Dalan.Admin.Settings;
using Microsoft.EntityFrameworkCore;

namespace Dalan.Admin.Data;

public sealed class AdminDb(DbContextOptions<AdminDb> options) : DbContext(options)
{
    public const string Schema = "admin";

    public DbSet<AccessLogEntry> AccessLog => Set<AccessLogEntry>();
    public DbSet<SettingRow> Settings => Set<SettingRow>();
    public DbSet<AuditEntry> Audit => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<AccessLogEntry>(entry =>
        {
            entry.ToTable("access_log");
            // Text ("User", "Bot", "Phone"), so the table can be read with plain SQL until the admin screen exists.
            entry.Property(e => e.Actor).HasConversion<string>();
            // "The addresses of this user or bot", newest first: the question a complaint asks.
            entry.HasIndex(e => new { e.Actor, e.ActorId, e.At });
            // "Who asked for the codes sent to this number" (ADM-04).
            entry.HasIndex(e => e.Phone).HasFilter("phone IS NOT NULL");
            // For the retention delete (ADM-03).
            entry.HasIndex(e => e.At);
            // ADM-10: "who else used this address".
            entry.HasIndex(e => e.Address);
        });

        modelBuilder.Entity<SettingRow>(setting =>
        {
            setting.ToTable("settings");
            setting.HasKey(s => s.Key);
        });

        modelBuilder.Entity<AuditEntry>(entry => entry.ToTable("audit_log"));
    }
}
