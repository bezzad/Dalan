using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Dalan.Admin.PublicPages;

/// <summary>
/// ADM-15: one plain page for AI agents. The text is a file embedded in this assembly; the two addresses come from
/// the settings on each call, so a panel change shows at once. No session, no access log row (it knows no caller).
/// </summary>
internal static class LlmsTxtEndpoint
{
    private static readonly string Template = ReadTemplate();

    public static void MapLlmsTxt(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/llms.txt", (IOptionsMonitor<AdminOptions> options) => Results.Text(
            Template.Replace("{site}", options.CurrentValue.SiteUrl, StringComparison.Ordinal)
                .Replace("{api}", options.CurrentValue.BotApiUrl, StringComparison.Ordinal),
            "text/plain; charset=utf-8"));

    private static string ReadTemplate()
    {
        using var stream = typeof(LlmsTxtEndpoint).Assembly.GetManifestResourceStream("Dalan.Admin.llms.txt")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
