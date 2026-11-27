namespace Dalan.Relay.Contracts;

/// <summary>
/// REL-57 and SEC-16: the host that shows HTML files of a chat, away from the app's own origin.
/// Sites asks it for the certificate question (SITE-17).
/// </summary>
public interface IFileViewHost
{
    /// <summary>The view host without a scheme, like "view.dalans.ir"; null when the viewer is off.</summary>
    string? Host { get; }

    /// <summary>The view host's origin, like "https://view.dalans.ir"; null when the viewer is off.</summary>
    string? Origin { get; }
}
