using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dalan.Relay.Files;

/// <summary>The bytes of the files, in one flat folder (Relay:FileFolder). The table relay.files says what each one is.</summary>
public sealed class FileStore(IOptionsMonitor<RelayOptions> options, IHostEnvironment environment)
{
    private static readonly FileExtensionContentTypeProvider Types = new();

    private string Folder => Path.Combine(environment.ContentRootPath, options.CurrentValue.FileFolder);

    public long MaxBytes => options.CurrentValue.MaxFileSizeMb * 1024L * 1024L;

    /// <summary>Copies an upload to disk under a new random id. Returns null when it is bigger than the limit (nothing is kept).</summary>
    public async Task<(string Id, long Size)?> SaveAsync(Stream content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Folder);
        var id = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));
        var path = PathOf(id);
        var part = path + ".part";
        long size = 0;
        try
        {
            await using (var file = new FileStream(part, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    size += read;
                    if (size > MaxBytes) break;
                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }

            if (size > MaxBytes)
            {
                File.Delete(part);
                return null;
            }

            File.Move(part, path);
            return (id, size);
        }
        catch
        {
            File.Delete(part);
            throw;
        }
    }

    /// <summary>Null when the file is not on disk.</summary>
    public Stream? Open(string id)
    {
        try
        {
            return new FileStream(PathOf(id), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    public void Delete(string id) => File.Delete(PathOf(id));

    /// <summary>The type the uploader gave, or the one its name says when it gave none (or only "bytes").</summary>
    public static string TypeOf(string? given, string name)
    {
        var type = given?.Split(';')[0].Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(type) && type != "application/octet-stream") return type;
        return Types.TryGetContentType(name, out var byName) ? byName : "application/octet-stream";
    }

    /// <summary>An id is our own base64url text, so it cannot leave the folder; anything else is not a file of ours.</summary>
    private string PathOf(string id) =>
        id.Length == 22 && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')
            ? Path.Combine(Folder, id)
            : throw new FileNotFoundException();
}
