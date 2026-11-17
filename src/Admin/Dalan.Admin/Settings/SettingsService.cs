using Dalan.Admin.Contracts;
using Dalan.Admin.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dalan.Admin.Settings;

/// <summary>ADM-07, ADM-08: stores the admin's values and reloads them into the configuration.</summary>
public sealed partial class SettingsService(
    IDbContextFactory<AdminDb> dbFactory,
    IConfiguration configuration,
    SettingsCatalog catalog,
    IAuditLog audit,
    IOptionsMonitor<AdminOptions> options,
    TimeProvider time,
    ILogger<SettingsService> logger) : IDisposable
{
    private readonly SemaphoreSlim _writing = new(1, 1);

    /// <summary>Reads the table into the configuration. A value that no longer fits is skipped with a warning.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Settings.AsNoTracking().ToListAsync(cancellationToken);
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var entry = catalog.Find(row.Key);
            var value = entry is null ? null : SettingsCatalog.Clean(entry.Kind, row.Value, int.MaxValue);
            if (value is null) LogSkipped(row.Key);
            else values[entry!.Key] = value;
        }

        Provider.Replace(values);
    }

    public enum Outcome
    {
        Done,
        NotFound,
        BadValue,
    }

    public async Task<(Outcome Outcome, SettingEntry? Entry, string? Rule)> SetAsync(
        long adminId, string key, string value, CancellationToken cancellationToken)
    {
        if (catalog.Find(key) is not { } entry) return (Outcome.NotFound, null, null);

        var max = options.CurrentValue.MaxSettingLength;
        if (SettingsCatalog.Clean(entry.Kind, value, max) is not { } clean)
            return (Outcome.BadValue, entry, SettingsCatalog.Rule(entry.Kind, max));

        await _writing.WaitAsync(cancellationToken);
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var now = time.GetUtcNow();
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO admin.settings (key, value, updated_at, updated_by) VALUES ({entry.Key}, {clean}, {now}, {adminId})
                ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value, updated_at = EXCLUDED.updated_at, updated_by = EXCLUDED.updated_by
                """, cancellationToken);
            await LoadAsync(cancellationToken);
        }
        finally
        {
            _writing.Release();
        }

        await audit.RecordAsync(adminId, "setting.set", entry.Key, $"{entry.Value} -> {clean}", cancellationToken);
        return (Outcome.Done, catalog.Find(entry.Key), null);
    }

    public async Task<SettingEntry?> ResetAsync(long adminId, string key, CancellationToken cancellationToken)
    {
        if (catalog.Find(key) is not { } entry) return null;

        await _writing.WaitAsync(cancellationToken);
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            await db.Settings.Where(s => s.Key == entry.Key).ExecuteDeleteAsync(cancellationToken);
            await LoadAsync(cancellationToken);
        }
        finally
        {
            _writing.Release();
        }

        var after = catalog.Find(entry.Key)!;
        await audit.RecordAsync(adminId, "setting.reset", entry.Key, $"{entry.Value} -> {after.Value}", cancellationToken);
        return after;
    }

    public void Dispose() => _writing.Dispose();

    private DatabaseSettings Provider =>
        ((IConfigurationRoot)configuration).Providers.OfType<DatabaseSettings>().FirstOrDefault()
        ?? throw new InvalidOperationException("The host must call AddAdminSettings() on its configuration.");

    [LoggerMessage(LogLevel.Warning, "The stored setting {Key} is not editable or its value does not fit any more; it is skipped.")]
    private partial void LogSkipped(string key);
}
