using System.Net;

namespace Dalan.Identity.Keys;

/// <summary>AUTH-38: the address list of an access key. Each entry is an IPv4 or IPv6 address, or a range in CIDR form.</summary>
internal static class KeyAddresses
{
    /// <summary>
    /// The entry in its short form ("80.244.7.0/24", "2a01::1"), or null when it is not an address or a range.
    /// A range must start at its first address: "80.244.7.1/24" is refused, so the list never means more than was meant.
    /// </summary>
    public static string? Normalize(string? entry)
    {
        var text = entry?.Trim() ?? "";
        if (!text.Contains('/', StringComparison.Ordinal))
            return IPAddress.TryParse(text, out var address) && IsPlain(text) ? Plain(address).ToString() : null;
        return IPNetwork.TryParse(text, out var network) && IPAddress.TryParse(text[..text.IndexOf('/', StringComparison.Ordinal)], out var start)
            && start.Equals(network.BaseAddress) ? network.ToString() : null;
    }

    /// <summary>True when the list is empty or has the address.</summary>
    public static bool Allows(string[] list, IPAddress? address)
    {
        if (list.Length == 0) return true;
        if (address is null) return false;
        address = Plain(address);
        foreach (var entry in list)
        {
            if (entry.Contains('/', StringComparison.Ordinal))
            {
                if (IPNetwork.TryParse(entry, out var network) && network.Contains(address)) return true;
            }
            else if (IPAddress.TryParse(entry, out var one) && one.Equals(address))
            {
                return true;
            }
        }

        return false;
    }

    private static IPAddress Plain(IPAddress address) => address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    /// <summary>IPAddress.TryParse also takes "1" or "1.2" as IPv4; only the full four parts or an IPv6 address count.</summary>
    private static bool IsPlain(string text) =>
        text.Contains(':', StringComparison.Ordinal) || text.Split('.').Length == 4;
}
