using Dalan.Admin.AdminApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace Dalan.Admin.PublicPages;

internal sealed record PublicSettingsJson(int HistoryMessages, int HistoryDays, int AccessLogDays, int FileDays, int UpdateExpirySeconds);

/// <summary>
/// ADM-14: the numbers the privacy page and the FAQ print (CLI-27). Admin knows every setting, so it answers for all
/// services. Read from the configuration on each call, so a panel change shows at once. No session, no access log row
/// (it knows no caller); the per-address limit (SEC-02) holds.
/// </summary>
internal static class PublicSettingsEndpoint
{
    public static void MapPublicSettings(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/api/public/settings", (IConfiguration configuration) => AdminJson.Ok(new PublicSettingsJson(
            configuration.GetValue<int>("Relay:HistoryMessages"),
            configuration.GetValue<int>("Relay:HistoryDays"),
            configuration.GetValue<int>("Admin:AccessLogRetentionDays"),
            configuration.GetValue<int>("Relay:FileRetentionDays"),
            configuration.GetValue<int>("Relay:UpdateExpirySeconds"))));
}
