using System.Text.RegularExpressions;

namespace Dalan.Identity.Users;

public static partial class PhoneNumber
{
    /// <summary>Turns an Iranian mobile number (09121234567 or +989121234567) into +989121234567. Returns null for anything else.</summary>
    public static string? Normalize(string? input)
    {
        var match = IranMobile().Match(input?.Trim() ?? "");
        return match.Success ? "+98" + match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"^(?:0|\+98)(9[0-9]{9})$")]
    private static partial Regex IranMobile();
}
