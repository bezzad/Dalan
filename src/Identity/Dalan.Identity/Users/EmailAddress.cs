using System.Text.RegularExpressions;

namespace Dalan.Identity.Users;

public static partial class EmailAddress
{
    /// <summary>Longest address that SMTP allows.</summary>
    private const int MaxLength = 254;

    /// <summary>Trims and lower-cases an address like name@host.tld (AUTH-24). Returns null for anything else.</summary>
    public static string? Normalize(string? input)
    {
#pragma warning disable CA1308 // Addresses are kept in lower case, so one person cannot get two accounts by case (AUTH-01).
        var email = input?.Trim().ToLowerInvariant() ?? "";
#pragma warning restore CA1308
        return email.Length <= MaxLength && Shape().IsMatch(email) ? email : null;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s.]+$")]
    private static partial Regex Shape();
}
