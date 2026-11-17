using System.Text.RegularExpressions;

namespace Dalan.Telegram;

/// <summary>TG-06: many Telegram libraries find a command only through its "bot_command" entity.</summary>
internal static partial class BotCommands
{
    // "/name" or "/name@bot", at the start of the text or after white space. C# counts in UTF-16 units, as Telegram does.
    [GeneratedRegex(@"(?<!\S)/[A-Za-z0-9_]{1,32}(?![A-Za-z0-9_])(@[A-Za-z0-9_]+)?", RegexOptions.CultureInvariant)]
    private static partial Regex Command();

    /// <summary>Returns null when the text has no command, so the field is left out.</summary>
    public static IReadOnlyList<EntityJson>? Find(string text)
    {
        var matches = Command().Matches(text);
        return matches.Count == 0 ? null : matches.Select(m => new EntityJson("bot_command", m.Index, m.Length)).ToList();
    }
}
