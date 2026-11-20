using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Dalan.Sites.Downloads;

/// <summary>
/// AGT-10: the one-line install. The agent files come from Dalan itself (from the folder <see cref="SitesOptions.AgentFolder"/>,
/// which the deploy fills), so a user with no international internet can install it.
/// </summary>
internal static class AgentDownloads
{
    /// <summary>The only files served. CI builds the Linux and Windows ones (DEL-08); macOS when a macOS machine is there.</summary>
    internal static readonly string[] Files = ["dalan-linux-x64", "dalan-macos-arm64", "dalan-macos-x64", "dalan-windows-x64.exe"];

    public static void MapAgentDownloads(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/install.sh", (IConfiguration configuration) => Script("install.sh", configuration, "text/x-shellscript"));
        endpoints.MapGet("/install.ps1", (IConfiguration configuration) => Script("install.ps1", configuration, "text/plain"));

        endpoints.MapGet("/downloads/{file}", (string file, IOptionsMonitor<SitesOptions> options) =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, options.CurrentValue.AgentFolder, file);
            return Files.Contains(file) && File.Exists(path)
                ? Results.File(path, "application/octet-stream", file)
                : SitesJson.Error(StatusCodes.Status404NotFound, "file_not_found", "There is no such agent file on this server.");
        });
    }

    /// <summary>The script with this server's own address in it.</summary>
    private static IResult Script(string name, IConfiguration configuration, string contentType)
    {
        using var stream = typeof(AgentDownloads).Assembly.GetManifestResourceStream($"Dalan.Sites.Downloads.{name}")
            ?? throw new InvalidOperationException($"{name} is not embedded");
        using var reader = new StreamReader(stream);
        var siteUrl = (configuration["Admin:SiteUrl"] ?? "").TrimEnd('/');
        return Results.Text(reader.ReadToEnd().Replace("__DALAN_URL__", siteUrl, StringComparison.Ordinal), contentType + "; charset=utf-8");
    }
}
