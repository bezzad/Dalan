namespace Dalan.Sites.Names;

/// <summary>SITE-02: a site name is one DNS label, in lower case.</summary>
public static class SiteName
{
    private const int MinLength = 3;

    /// <summary>The name in lower case, or null when it is not a good site name.</summary>
    public static string? Normalize(string? name, SitesOptions options)
    {
        if (name is null) return null;
        var lower = name.Trim().ToLowerInvariant();
        if (lower.Length < MinLength || lower.Length > options.MaxNameLength) return null;
        if (lower[0] == '-' || lower[^1] == '-') return null;
        if (!lower.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-')) return null;
        return IsReserved(lower, options) ? null : lower;
    }

    private static bool IsReserved(string name, SitesOptions options) =>
        options.ReservedNames.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Contains(name, StringComparer.OrdinalIgnoreCase);
}
