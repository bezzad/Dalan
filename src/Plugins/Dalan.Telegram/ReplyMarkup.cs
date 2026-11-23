using System.Text.Json;
using Dalan.Relay.Contracts;

namespace Dalan.Telegram;

/// <summary>TG-08 and TG-14: Telegram's reply_markup, turned into the keyboards of the core.</summary>
internal static class ReplyMarkup
{
    // Inline buttons that ask for something Dalan does not have (inline mode, games, payments, web apps, logins).
    private static readonly string[] UnsupportedInlineFields =
    [
        "switch_inline_query", "switch_inline_query_current_chat", "switch_inline_query_chosen_chat",
        "login_url", "web_app", "callback_game", "pay", "copy_text",
    ];

    /// <summary>
    /// Returns null when all is well, otherwise what is wrong.
    /// <paramref name="keyboard"/>: null = the message does not change the reply keyboard, no rows = remove it.
    /// <paramref name="inline"/>: the buttons under the message, or null.
    /// </summary>
    public static string? Read(
        TelegramParameters parameters, out List<List<string>>? keyboard, out List<List<InlineButton>>? inline, out KeyboardOptions? options)
    {
        keyboard = null;
        inline = null;
        options = null;
        if (!parameters.TryObject("reply_markup", out var given)) return "can't parse reply markup: it must be a JSON object";
        if (given is not { } markup) return null;

        if (markup.TryGetProperty("inline_keyboard", out var inlineRows)) return ReadInline(inlineRows, out inline);

        if (markup.TryGetProperty("remove_keyboard", out var remove) && remove.ValueKind == JsonValueKind.True)
        {
            keyboard = [];
            return null;
        }

        if (!markup.TryGetProperty("keyboard", out var rows))
            return "this kind of reply markup is not supported by Dalan; use inline_keyboard, keyboard or remove_keyboard";

        if (ReadRows<ReplyButton>(rows, ReplyButtonOf, out var buttons) is { } problem) return problem;

        keyboard = buttons!.ConvertAll(row => row.ConvertAll(b => b.Text));
        var styles = buttons.ConvertAll(row => row.ConvertAll(b => b.Style));
        options = new KeyboardOptions(
            Flag(markup, "is_persistent"), Flag(markup, "resize_keyboard"), Flag(markup, "one_time_keyboard"),
            styles.Exists(row => row.Exists(style => style is not null)) ? styles : null);
        return null;
    }

    private static bool Flag(JsonElement markup, string name) =>
        markup.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    /// <summary>For an edit: only inline buttons, or none.</summary>
    public static string? ReadInline(TelegramParameters parameters, out List<List<InlineButton>>? inline)
    {
        if (Read(parameters, out var keyboard, out inline, out _) is { } problem) return problem;
        return keyboard is null ? null : "inline keyboard expected";
    }

    private static string? ReadInline(JsonElement rows, out List<List<InlineButton>>? inline) =>
        ReadRows(rows, InlineButtonOf, out inline);

    private static string? ReadRows<T>(JsonElement rows, ReadButton<T> read, out List<List<T>>? keyboard)
    {
        keyboard = null;
        if (rows.ValueKind != JsonValueKind.Array) return "can't parse reply markup: a keyboard must be a list of rows";

        var result = new List<List<T>>();
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Array) return "can't parse reply markup: each keyboard row must be a list of buttons";

            var buttons = new List<T>();
            foreach (var button in row.EnumerateArray())
            {
                if (read(button, out var value) is { } problem) return problem;
                buttons.Add(value);
            }

            result.Add(buttons);
        }

        keyboard = result;
        return null;
    }

    private delegate string? ReadButton<T>(JsonElement button, out T value);

    private readonly record struct ReplyButton(string Text, string? Style);

    /// <summary>A reply button is a text, or an object with "text" and an optional "style". A tap sends that text.</summary>
    private static string? ReplyButtonOf(JsonElement button, out ReplyButton value)
    {
        value = new ReplyButton("", null);
        if (button.ValueKind == JsonValueKind.String)
        {
            value = new ReplyButton(button.GetString()!, null);
            return null;
        }

        if (TextOf(button) is not { } text) return "can't parse reply markup: a keyboard button must be a text or an object with text";

        // A button that asks the user for a contact, a place or a web app would wait for an answer Dalan cannot give.
        foreach (var field in button.EnumerateObject())
        {
            if (field.Name.StartsWith("request_", StringComparison.Ordinal) || field.Name == "web_app")
                return $"keyboard button field {field.Name} is not supported by Dalan";
        }

        // icon_custom_emoji_id only decorates the button; Dalan ignores it. A wrong style is refused by the core.
        value = new ReplyButton(text, StyleOf(button));
        return null;
    }

    /// <summary>An inline button: text and callback_data or url. The core checks the limits (REL-20).</summary>
    private static string? InlineButtonOf(JsonElement button, out InlineButton value)
    {
        value = new InlineButton("");
        if (TextOf(button) is not { } text) return "can't parse inline keyboard button: it must be an object with text";

        foreach (var field in button.EnumerateObject())
        {
            if (UnsupportedInlineFields.Contains(field.Name)) return $"inline keyboard button field {field.Name} is not supported by Dalan";
        }

        value = new InlineButton(text, StringOf(button, "callback_data"), StringOf(button, "url"), StyleOf(button));
        return null;
    }

    private static string? StyleOf(JsonElement button) => StringOf(button, "style");

    private static string? TextOf(JsonElement button) => StringOf(button, "text");

    private static string? StringOf(JsonElement button, string name) =>
        button.ValueKind == JsonValueKind.Object && button.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
