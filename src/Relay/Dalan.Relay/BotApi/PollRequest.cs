using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace Dalan.Relay.BotApi;

/// <summary>The parameters of one get-updates call, with the limits applied (Q-06, Q-08).</summary>
public sealed record PollRequest(long Offset, int Limit, TimeSpan Timeout)
{
    /// <summary>Returns null and an error text when a parameter is not valid.</summary>
    public static PollRequest? Parse(IQueryCollection query, RelayOptions options, out string error)
    {
        if (!TryRead(query, "offset", out var offset, out error)
            || !TryRead(query, "limit", out var limit, out error)
            || !TryRead(query, "timeout", out var timeout, out error))
        {
            return null;
        }

        if (limit < 1)
        {
            error = "'limit' must be 1 or more.";
            return null;
        }

        return new PollRequest(
            offset ?? 0,
            (int)Math.Min(limit ?? options.MaxUpdatesPerPoll, options.MaxUpdatesPerPoll),
            TimeSpan.FromSeconds(Math.Min(timeout ?? options.PollTimeoutDefaultSeconds, options.PollTimeoutMaxSeconds)));
    }

    private static bool TryRead(IQueryCollection query, string name, out long? value, out string error)
    {
        value = null;
        error = "";
        if (!query.TryGetValue(name, out var text)) return true;

        if (long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            value = number;
            return true;
        }

        error = $"'{name}' must be a whole number, 0 or more.";
        return false;
    }
}
