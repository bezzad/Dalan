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
    public static string? Read(TelegramParameters parameters, out List<List<string>>? keyboard, out List<List<InlineButton>>? inline)
    {
        keyboard = null;
        inline = null;
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

        return ReadRows(rows, ButtonText, out keyboard);
    }

    /// <summary>For an edit: only inline buttons, or none.</summary>
    public static string? ReadInline(TelegramParameters parameters, out List<List<InlineButton>>? inline)
    {
        if (Read(parameters, out var keyboard, out inline) is { } problem) return problem;
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

    /// <summary>A reply button is a text, or an object with "text". A tap sends that text.</summary>
    private static string? ButtonText(JsonElement button, out string text)
    {
        text = "";
        if (button.ValueKind == JsonValueKind.String)
        {
            text = button.GetString()!;
            return null;
        }

        if (TextOf(button) is not { } value) return "can't parse reply markup: a keyboard button must be a text or an object with text";

        // A button that asks the user for a contact, a place or a web app would wait for an answer Dalan cannot give.
        foreach (var field in button.EnumerateObject())
        {
            if (field.Name.StartsWith("request_", StringComparison.Ordinal) || field.Name == "web_app")
                return $"keyboard button field {field.Name} is not supported by Dalan";
        }

        text = value;
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

        value = new InlineButton(text, StringOf(button, "callback_data"), StringOf(button, "url"));
        return null;
    }

    private static string? TextOf(JsonElement button) => StringOf(button, "text");

    private static string? StringOf(JsonElement button, string name) =>
        button.ValueKind == JsonValueKind.Object && button.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
