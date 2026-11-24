using Dalan.Relay.Contracts;

namespace Dalan.Relay.Chats;

/// <summary>REL-53: the formatting rules of a text.</summary>
internal static class TextEntities
{
    private static readonly HashSet<string> Types = new(StringComparer.Ordinal)
    {
        "bold", "italic", "underline", "strikethrough", "spoiler", "code", "pre", "text_link", "blockquote", "expandable_blockquote",
    };

    /// <summary>
    /// True when the entities fit the text and the rules; <paramref name="stored"/> is then what to keep
    /// (a url only on text_link, a language only on pre; null for none).
    /// </summary>
    public static bool TryCheck(List<TextEntity>? entities, string text, RelayOptions options, out List<MessageEntity>? stored)
    {
        stored = null;
        if (entities is null or { Count: 0 }) return true;
        if (entities.Count > options.MaxTextEntities) return false;

        foreach (var entity in entities)
        {
            if (entity is null || !Types.Contains(entity.Type) || entity.Offset < 0 || entity.Length <= 0
                || (long)entity.Offset + entity.Length > text.Length)
            {
                return false;
            }

            if (entity.Type == "text_link" && !IsWebLink(entity.Url, options)) return false;
        }

        // Two entities are apart, or one holds the other, so a client can draw them as a tree.
        var sorted = entities.OrderBy(e => e.Offset).ThenByDescending(e => e.Length).ToList();
        var open = new Stack<int>();
        foreach (var entity in sorted)
        {
            while (open.Count > 0 && open.Peek() <= entity.Offset) open.Pop();
            var end = entity.Offset + entity.Length;
            if (open.Count > 0 && end > open.Peek()) return false;
            open.Push(end);
        }

        stored = sorted.ConvertAll(e => new MessageEntity
        {
            Type = e.Type,
            Offset = e.Offset,
            Length = e.Length,
            Url = e.Type == "text_link" ? e.Url : null,
            Language = e.Type == "pre" && !string.IsNullOrEmpty(e.Language) ? e.Language : null,
        });
        return true;
    }

    /// <summary>REL-20 and REL-53: an absolute http or https address within the length limit.</summary>
    public static bool IsWebLink(string? url, RelayOptions options) =>
        url is not null && url.Length <= options.MaxButtonUrlLength
        && Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
