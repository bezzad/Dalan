using System.Globalization;
using Dalan.Identity.Contracts;
using Dalan.Notification.Contracts;
using Microsoft.Extensions.Logging;

namespace Dalan.Sites.Names;

/// <summary>
/// SITE-25: each new site name goes to every admin's inbox (NTF-12), so the admin can check the site.
/// Persian text with the address and the name inside, as the app is Persian first.
/// </summary>
public sealed partial class NewSiteNotice(IInbox inbox, IUserDirectory users, ILogger<NewSiteNotice> logger)
{
    public async Task TellAdminsAsync(long builderId, IReadOnlyList<string> names, SitesOptions settings, CancellationToken cancellationToken)
    {
        try
        {
            var admins = await users.AdminIdsAsync(cancellationToken);
            if (admins.Count == 0) return;
            var builder = (await users.FindAsync(builderId, cancellationToken))?.DisplayName ?? "?";
            var text = string.Create(CultureInfo.InvariantCulture, $"توسعه‌دهنده: {builder} (شناسه {builderId})");
            var link = string.Create(CultureInfo.InvariantCulture, $"/dashboard/admin/users/{builderId}");
            foreach (var name in names)
            foreach (var admin in admins)
                await inbox.AddAsync(admin, $"سایت جدید: {name}.{settings.Domain}", text, link, cancellationToken);
        }
#pragma warning disable CA1031 // The site is made; a notice that failed must not fail the builder's call.
        catch (Exception exception) when (exception is not OperationCanceledException)
#pragma warning restore CA1031
        {
            LogFailed(exception.GetType().Name);
        }
    }

    [LoggerMessage(LogLevel.Error, "The admins could not be told of a new site ({Error}).")]
    private partial void LogFailed(string error);
}
