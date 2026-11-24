using System.Globalization;
using System.Text;
using Dalan.Relay.Contracts;

namespace Dalan.Telegram;

/// <summary>
/// TG-25: Telegram's parse_mode "HTML", turned into plain text and entities (REL-53). Offsets count UTF-16 code units,
/// as a .NET string does. Only Telegram's tags are allowed; anything else is an error, as in Telegram.
/// </summary>
internal static class TelegramHtml
{
    private sealed class Open(string tag, string? type, int start)
    {
        public string Tag { get; } = tag;
        public string? Type { get; set; } = type;
        public int Start { get; } = start;
        public string? Url { get; init; }
        public string? Language { get; set; }
    }

    /// <summary>Null when all is well; otherwise what is wrong, to follow "can't parse entities: ".</summary>
    public static string? Parse(string html, out string text, out List<TextEntity> entities)
    {
        var output = new StringBuilder(html.Length);
        var found = new List<TextEntity>();
        var open = new Stack<Open>();
        text = "";
        entities = found;

        var i = 0;
        while (i < html.Length)
        {
            var c = html[i];
            if (c == '&')
            {
                i = Reference(html, i, output);
                continue;
            }

            if (c != '<')
            {
                output.Append(c);
                i++;
                continue;
            }

            var end = html.IndexOf('>', i + 1);
            if (end < 0) return $"unclosed start tag at byte offset {ByteOffset(html, i)}";

            var inside = html[(i + 1)..end];
            if (inside.StartsWith('/'))
            {
                var name = inside[1..].Trim().ToLowerInvariant();
                if (open.Count == 0 || open.Peek().Tag != name)
                    return $"unexpected end tag \"{name}\" at byte offset {ByteOffset(html, i)}";

                var closed = open.Pop();
                // <pre><code class="language-x"> is one pre entity with a language.
                if (closed.Tag == "code" && closed.Language is { } language && open.TryPeek(out var parent) && parent.Tag == "pre")
                    parent.Language = language;
                else if (closed.Type is { } type && output.Length > closed.Start)
                    found.Add(new TextEntity(type, closed.Start, output.Length - closed.Start, closed.Url, closed.Type == "pre" ? closed.Language : null));
            }
            else if (StartTag(inside, output.Length) is { } started)
            {
                open.Push(started);
            }
            else
            {
                var name = inside.Split([' ', '\t', '\n', '\r', '/'], 2)[0];
                return $"unsupported start tag \"{name}\" at byte offset {ByteOffset(html, i)}";
            }

            i = end + 1;
        }

        if (open.Count > 0) return $"can't find end tag corresponding to start tag \"{open.Peek().Tag}\"";

        text = output.ToString();
        return null;
    }

    /// <summary>A start tag Telegram knows, or null. A type of null keeps the text with no entity (tg-emoji, a link Dalan cannot open).</summary>
    private static Open? StartTag(string inside, int start)
    {
        var parts = inside.Trim();
        var space = parts.IndexOfAny([' ', '\t', '\n', '\r']);
        var name = (space < 0 ? parts : parts[..space]).ToLowerInvariant();
        var attributes = Attributes(space < 0 ? "" : parts[space..]);

        string? Attribute(string key) => attributes.TryGetValue(key, out var value) ? value : null;

        return name switch
        {
            "b" or "strong" => new Open(name, "bold", start),
            "i" or "em" => new Open(name, "italic", start),
            "u" or "ins" => new Open(name, "underline", start),
            "s" or "strike" or "del" => new Open(name, "strikethrough", start),
            "tg-spoiler" => new Open(name, "spoiler", start),
            "span" when Attribute("class") == "tg-spoiler" => new Open(name, "spoiler", start),
            "pre" => new Open(name, "pre", start),
            "code" => new Open(name, "code", start)
            {
                Language = Attribute("class") is { } css && css.StartsWith("language-", StringComparison.Ordinal) ? css["language-".Length..] : null,
            },
            "blockquote" => new Open(name, attributes.ContainsKey("expandable") ? "expandable_blockquote" : "blockquote", start),
            "tg-emoji" => new Open(name, null, start),
            // A link that is not http or https (for example tg://user?id=) keeps its text and drops the link.
            "a" => Attribute("href") is { } href && IsWebLink(href)
                ? new Open(name, "text_link", start) { Url = href }
                : new Open(name, null, start),
            _ => null,
        };
    }

    private static bool IsWebLink(string href) =>
        Uri.TryCreate(href, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>name="value", name='value', name=value or a bare name; values may have character references.</summary>
    private static Dictionary<string, string> Attributes(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var i = 0;
        while (i < text.Length)
        {
            while (i < text.Length && (char.IsWhiteSpace(text[i]) || text[i] == '/')) i++;
            var nameStart = i;
            while (i < text.Length && text[i] != '=' && !char.IsWhiteSpace(text[i])) i++;
            if (i == nameStart) break;

            var name = text[nameStart..i];
            var value = "";
            if (i < text.Length && text[i] == '=')
            {
                i++;
                if (i < text.Length && text[i] is '"' or '\'')
                {
                    var quote = text[i];
                    var close = text.IndexOf(quote, i + 1);
                    if (close < 0) close = text.Length;
                    value = text[(i + 1)..close];
                    i = close + 1;
                }
                else
                {
                    var valueStart = i;
                    while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
                    value = text[valueStart..i];
                }
            }

            var decoded = new StringBuilder();
            for (var j = 0; j < value.Length;)
            {
                if (value[j] == '&') j = Reference(value, j, decoded);
                else decoded.Append(value[j++]);
            }

            result[name] = decoded.ToString();
        }

        return result;
    }

    /// <summary>
    /// Writes the character of the reference at <paramref name="at"/> and returns the index after it.
    /// Telegram knows &amp;lt; &amp;gt; &amp;amp; &amp;quot; and numeric references; any other &amp; stays as it is.
    /// </summary>
    private static int Reference(string text, int at, StringBuilder output)
    {
        var end = text.IndexOf(';', at + 1);
        if (end > at + 1 && end - at <= 12)
        {
            var name = text[(at + 1)..end];
            string? value = name switch
            {
                "lt" => "<",
                "gt" => ">",
                "amp" => "&",
                "quot" => "\"",
                _ when name.StartsWith("#x", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(name[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var hex) => CodePoint(hex),
                _ when name.StartsWith('#')
                    && int.TryParse(name[1..], NumberStyles.None, CultureInfo.InvariantCulture, out var number) => CodePoint(number),
                _ => null,
            };
            if (value is not null)
            {
                output.Append(value);
                return end + 1;
            }
        }

        output.Append('&');
        return at + 1;
    }

    private static string? CodePoint(int value) =>
        value is > 0 and <= 0x10FFFF and not (>= 0xD800 and <= 0xDFFF) ? char.ConvertFromUtf32(value) : null;

    private static int ByteOffset(string text, int index) => Encoding.UTF8.GetByteCount(text.AsSpan(0, index));
}
