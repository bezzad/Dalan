using System.Globalization;
using System.Text.RegularExpressions;
using Dalan.Admin.AdminApi;
using Dalan.Admin.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dalan.Admin.ServerBackups;

internal sealed record BackupJson(string Name, long Size, DateTimeOffset At, bool Uploaded);

internal sealed record BackupsResponse(
    IEnumerable<BackupJson> Backups, bool BackupWaiting, bool RestoreWaiting, string? BackupStatus, string? RestoreStatus);

internal sealed record RestoreRequest(string? Confirm);

/// <summary>
/// ADM-21, ADM-22: the backups of the server. This process only reads the archives and writes request files; root does the
/// rest (deploy/backup.sh, deploy/restore.sh, started by dalan-backup.path and dalan-restore.path, OPS-20).
/// </summary>
internal static partial class BackupEndpoints
{
    /// <summary>The word the admin types before a restore.</summary>
    public const string Confirm = "RESTORE";

    /// <summary>The names backup.sh and the upload give; restore.sh takes only these (no "/", no "..").</summary>
    [GeneratedRegex(@"^dalan-[A-Za-z0-9-]+\.tar\.gz$")]
    private static partial Regex ArchiveName();

    public static void MapBackups(this RouteGroupBuilder admin)
    {
        admin.MapGet("/backups", List);
        admin.MapPost("/backups", StartAsync);
        admin.MapPost("/backups/upload", UploadAsync);
        admin.MapGet("/backups/{name}", DownloadAsync);
        admin.MapPost("/backups/{name}/restore", RestoreAsync);
    }

    private static string Folder(IHostEnvironment environment, IOptionsMonitor<AdminOptions> options) =>
        Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.CurrentValue.BackupFolder));

    private static IEnumerable<BackupJson> Archives(string folder, bool uploaded) =>
        Directory.Exists(folder)
            ? new DirectoryInfo(folder).EnumerateFiles("dalan-*.tar.gz")
                .Where(f => ArchiveName().IsMatch(f.Name) && f.LinkTarget is null)
                .Select(f => new BackupJson(f.Name, f.Length, new DateTimeOffset(f.LastWriteTimeUtc, TimeSpan.Zero), uploaded))
            : [];

    /// <summary>The archive with this name, from the folder or its uploads; null for any other name.</summary>
    private static string? Find(string folder, string name)
    {
        if (!ArchiveName().IsMatch(name)) return null;
        return new[] { Path.Combine(folder, name), Path.Combine(folder, "uploads", name) }.FirstOrDefault(File.Exists);
    }

    private static string? Status(string path) => File.Exists(path) ? File.ReadAllText(path).Trim() : null;

    private static IResult List(IHostEnvironment environment, IOptionsMonitor<AdminOptions> options)
    {
        var folder = Folder(environment, options);
        var requests = Path.Combine(folder, "requests");
        return AdminJson.Ok(new BackupsResponse(
            [.. Archives(folder, false).Concat(Archives(Path.Combine(folder, "uploads"), true)).OrderByDescending(b => b.At)],
            File.Exists(Path.Combine(requests, "backup")), File.Exists(Path.Combine(requests, "restore")),
            Status(Path.Combine(folder, "backup-status")), Status(Path.Combine(folder, "restore-status"))));
    }

    private static async Task<IResult> StartAsync(
        HttpContext http, IHostEnvironment environment, IOptionsMonitor<AdminOptions> options, IAuditLog audit)
    {
        var requests = Directory.CreateDirectory(Path.Combine(Folder(environment, options), "requests")).FullName;
        await File.WriteAllTextAsync(Path.Combine(requests, "backup"), "", http.RequestAborted);
        await audit.RecordAsync(AdminFilter.UserId(http), "backup.start", "backup", null, http.RequestAborted);
        return Results.StatusCode(StatusCodes.Status202Accepted);
    }

    private static async Task<IResult> DownloadAsync(
        string name, HttpContext http, IHostEnvironment environment, IOptionsMonitor<AdminOptions> options, IAuditLog audit)
    {
        if (Find(Folder(environment, options), name) is not { } path) return NotFound();
        await audit.RecordAsync(AdminFilter.UserId(http), "backup.download", name, null, http.RequestAborted);
        return Results.File(path, "application/gzip", name, enableRangeProcessing: true);
    }

    /// <summary>ADM-22: the raw body is the archive; it is kept only when it is gzip and not bigger than the limit.</summary>
    private static async Task<IResult> UploadAsync(
        HttpContext http, IHostEnvironment environment, IOptionsMonitor<AdminOptions> options, IAuditLog audit, TimeProvider time)
    {
        var max = options.CurrentValue.BackupMaxUploadMb * 1024L * 1024L;
        if (http.Request.ContentLength > max) return TooLarge(options);
        if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit) limit.MaxRequestBodySize = max + 1;

        var uploads = Directory.CreateDirectory(Path.Combine(Folder(environment, options), "uploads")).FullName;
        var part = Path.Combine(uploads, $".upload-{Guid.NewGuid():N}.part");
        long size = 0;
        var gzip = false;
        try
        {
            await using (var file = new FileStream(part, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await http.Request.Body.ReadAsync(buffer, http.RequestAborted)) > 0)
                {
                    if (size == 0) gzip = read >= 2 && buffer[0] == 0x1f && buffer[1] == 0x8b;
                    if (!gzip) break;
                    size += read;
                    if (size > max) break;
                    await file.WriteAsync(buffer.AsMemory(0, read), http.RequestAborted);
                }
            }
            if (!gzip) return AdminJson.BadRequest("Send a backup archive (.tar.gz) as the body.");
            if (size > max) return TooLarge(options);

            var name = "dalan-upload-" + time.GetUtcNow().ToString("yyyy-MM-dd-HHmmss", CultureInfo.InvariantCulture) + ".tar.gz";
            var path = Path.Combine(uploads, name);
            File.Move(part, path, overwrite: true);
            await audit.RecordAsync(AdminFilter.UserId(http), "backup.upload", name,
                size.ToString(CultureInfo.InvariantCulture) + " bytes", http.RequestAborted);
            return Results.Json(new BackupJson(name, size, time.GetUtcNow(), true), AdminJson.Options,
                statusCode: StatusCodes.Status201Created);
        }
        finally
        {
            File.Delete(part);
        }
    }

    private static async Task<IResult> RestoreAsync(
        string name, HttpContext http, IHostEnvironment environment, IOptionsMonitor<AdminOptions> options, IAuditLog audit)
    {
        var folder = Folder(environment, options);
        if (Find(folder, name) is null) return NotFound();
        if (await AdminJson.ReadAsync<RestoreRequest>(http) is not { Confirm: Confirm })
            return AdminJson.Error(StatusCodes.Status400BadRequest, "confirm_needed",
                $$"""A restore replaces the whole server. Send JSON like {"confirm": "{{Confirm}}"}.""");

        var requests = Directory.CreateDirectory(Path.Combine(folder, "requests")).FullName;
        await File.WriteAllTextAsync(Path.Combine(requests, "restore"), name + "\n", http.RequestAborted);
        await audit.RecordAsync(AdminFilter.UserId(http), "backup.restore", name, null, http.RequestAborted);
        return Results.StatusCode(StatusCodes.Status202Accepted);
    }

    private static IResult NotFound() => AdminJson.NotFound("backup_not_found", "There is no backup with this name.");

    private static IResult TooLarge(IOptionsMonitor<AdminOptions> options) => AdminJson.Error(StatusCodes.Status413PayloadTooLarge,
        "too_large", $"A backup may be at most {options.CurrentValue.BackupMaxUploadMb} MB (Admin:BackupMaxUploadMb).");
}
