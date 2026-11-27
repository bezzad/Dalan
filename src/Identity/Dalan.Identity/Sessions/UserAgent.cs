namespace Dalan.Identity.Sessions;

/// <summary>AUTH-35: a short browser and system name from a user agent, for the session list. Empty when unknown.</summary>
internal static class UserAgent
{
    // The order matters: Edge and Opera also say "Chrome", Chrome also says "Safari".
    private static readonly (string Mark, string Name)[] Browsers =
    [
        ("Edg/", "Edge"), ("OPR/", "Opera"), ("SamsungBrowser/", "Samsung Internet"), ("Firefox/", "Firefox"),
        ("FxiOS/", "Firefox"), ("CriOS/", "Chrome"), ("Chrome/", "Chrome"), ("Safari/", "Safari"),
    ];

    // Android says "Linux", iOS says "like Mac OS X".
    private static readonly (string Mark, string Name)[] Systems =
    [
        ("Android", "Android"), ("iPhone", "iOS"), ("iPad", "iOS"), ("Windows", "Windows"), ("CrOS", "ChromeOS"),
        ("Mac OS X", "macOS"), ("Linux", "Linux"),
    ];

    public static (string Browser, string Os) Parse(string? userAgent) =>
        (Find(Browsers, userAgent), Find(Systems, userAgent));

    private static string Find((string Mark, string Name)[] names, string? userAgent) =>
        userAgent is null ? "" : names.FirstOrDefault(n => userAgent.Contains(n.Mark, StringComparison.Ordinal)).Name ?? "";
}
