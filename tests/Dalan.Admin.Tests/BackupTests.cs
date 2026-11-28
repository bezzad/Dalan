using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dalan.Testing;

namespace Dalan.Admin.Tests;

/// <summary>ADM-21, ADM-22: the backups in the admin door. The scripts that make and restore them are shell tests (OPS-11, OPS-19).</summary>
public sealed class BackupTests(PostgresServer postgres) : IDisposable
{
    private static CancellationToken Cancel => AdminApp.Cancel;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "dalan-backup-tests", Guid.NewGuid().ToString("N"));

    /// <summary>A little gzip file: the first bytes are what the upload checks.</summary>
    private static readonly byte[] Archive = [0x1f, 0x8b, 8, 0, 1, 2, 3, 4, 5, 6];

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private Task<DalanApp> StartAsync(params (string, string)[] settings) =>
        AdminApp.StartAsync(postgres, [("Admin:BackupFolder", _folder), .. settings]);

    private static async Task<string[]> AuditActionsAsync(HttpClient admin) =>
        [.. (await admin.JsonAsync("/api/admin/audit")).Rows().Select(r => r.GetProperty("action").GetString()!).Where(a => a.StartsWith("backup.", StringComparison.Ordinal)).Reverse()];

    private static ByteArrayContent Body(byte[] bytes)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/gzip");
        return content;
    }

    [Fact]
    [Trait("Rule", "ADM-21")]
    public async Task ADM21_make_and_download()
    {
        Directory.CreateDirectory(_folder);
        await File.WriteAllBytesAsync(Path.Combine(_folder, "dalan-2026-11-28-070000.tar.gz"), Archive, Cancel);
        await File.WriteAllTextAsync(Path.Combine(_folder, "backup-status"), "ok 2026-11-28T07:00:00Z dalan-2026-11-28-070000.tar.gz\n", Cancel);
        await using var app = await StartAsync();
        var (admin, _) = await app.AdminAsync();

        Assert.Equal(HttpStatusCode.Accepted, (await admin.PostAsync("/api/admin/backups", null, Cancel)).StatusCode);
        var list = await admin.JsonAsync("/api/admin/backups");
        var download = await admin.GetAsync("/api/admin/backups/dalan-2026-11-28-070000.tar.gz", Cancel);

        Assert.True(File.Exists(Path.Combine(_folder, "requests", "backup")));
        Assert.True(list.GetProperty("backup_waiting").GetBoolean());
        Assert.False(list.GetProperty("restore_waiting").GetBoolean());
        Assert.Equal("ok 2026-11-28T07:00:00Z dalan-2026-11-28-070000.tar.gz", list.GetProperty("backup_status").GetString());
        var backup = Assert.Single(list.Rows("backups"));
        Assert.Equal("dalan-2026-11-28-070000.tar.gz", backup.GetProperty("name").GetString());
        Assert.Equal(Archive.Length, backup.GetProperty("size").GetInt64());
        Assert.False(backup.GetProperty("uploaded").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(Archive, await download.Content.ReadAsByteArrayAsync(Cancel));
        Assert.Equal("dalan-2026-11-28-070000.tar.gz", download.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.Equal(["backup.start", "backup.download"], await AuditActionsAsync(admin));
    }

    [Theory]
    [Trait("Rule", "ADM-21")]
    [InlineData("..%2Fdalan.env")]
    [InlineData("dalan.env")]
    [InlineData("backup-status")]
    [InlineData("dalan-2026-11-28-080000.tar.gz")]
    public async Task ADM21_a_path_or_unknown_name_is_not_found(string name)
    {
        Directory.CreateDirectory(_folder);
        await File.WriteAllTextAsync(Path.Combine(_folder, "dalan.env"), "secret", Cancel);
        await File.WriteAllTextAsync(Path.Combine(_folder, "backup-status"), "ok", Cancel);
        await using var app = await StartAsync();
        var (admin, _) = await app.AdminAsync();

        var response = await admin.GetAsync($"/api/admin/backups/{name}", Cancel);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("backup_not_found", await response.ErrorCodeAsync());
    }

    [Fact]
    [Trait("Rule", "ADM-21")]
    public async Task ADM21_only_an_admin()
    {
        await using var app = await StartAsync();
        var (user, _) = await app.LoginAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsync("/api/admin/backups", null, Cancel)).StatusCode);
        Assert.False(Directory.Exists(Path.Combine(_folder, "requests")));
    }

    [Fact]
    [Trait("Rule", "ADM-22")]
    public async Task ADM22_restore_an_upload()
    {
        await using var app = await StartAsync();
        var (admin, _) = await app.AdminAsync();

        var upload = await (await admin.PostAsync("/api/admin/backups/upload", Body(Archive), Cancel)).JsonAsync();
        var name = upload.GetProperty("name").GetString()!;
        var restore = await admin.PostAsJsonAsync($"/api/admin/backups/{name}/restore", new { confirm = "RESTORE" }, Cancel);
        var list = await admin.JsonAsync("/api/admin/backups");

        Assert.Equal("dalan-upload-2026-11-16-120000.tar.gz", name);
        Assert.Equal(Archive, await File.ReadAllBytesAsync(Path.Combine(_folder, "uploads", name), Cancel));
        Assert.Equal(HttpStatusCode.Accepted, restore.StatusCode);
        Assert.Equal(name + "\n", await File.ReadAllTextAsync(Path.Combine(_folder, "requests", "restore"), Cancel));
        Assert.True(list.GetProperty("restore_waiting").GetBoolean());
        Assert.True(Assert.Single(list.Rows("backups")).GetProperty("uploaded").GetBoolean());
        Assert.Single(Directory.GetFiles(Path.Combine(_folder, "uploads")));
        Assert.Equal(["backup.upload", "backup.restore"], await AuditActionsAsync(admin));
    }

    [Theory]
    [Trait("Rule", "ADM-22")]
    [InlineData("""{"confirm": "restore"}""")]
    [InlineData("""{}""")]
    [InlineData("""not json""")]
    public async Task ADM22_no_confirm_no_restore(string body)
    {
        Directory.CreateDirectory(_folder);
        await File.WriteAllBytesAsync(Path.Combine(_folder, "dalan-2026-11-28-070000.tar.gz"), Archive, Cancel);
        await using var app = await StartAsync();
        var (admin, _) = await app.AdminAsync();

        var response = await admin.PostAsync("/api/admin/backups/dalan-2026-11-28-070000.tar.gz/restore",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"), Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("confirm_needed", await response.ErrorCodeAsync());
        Assert.False(File.Exists(Path.Combine(_folder, "requests", "restore")));
    }

    [Fact]
    [Trait("Rule", "ADM-22")]
    public async Task ADM22_not_gzip_or_too_large_is_not_kept()
    {
        await using var app = await StartAsync(("Admin:BackupMaxUploadMb", "1"));
        var (admin, _) = await app.AdminAsync();

        var text = await admin.PostAsync("/api/admin/backups/upload", Body("not an archive"u8.ToArray()), Cancel);
        var big = new byte[(1024 * 1024) + 10];
        Archive.CopyTo(big, 0);
        var large = await admin.PostAsync("/api/admin/backups/upload", Body(big), Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, text.StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, large.StatusCode);
        Assert.Empty(Directory.GetFiles(Path.Combine(_folder, "uploads")));
        Assert.Empty(await AuditActionsAsync(admin));
    }
}
