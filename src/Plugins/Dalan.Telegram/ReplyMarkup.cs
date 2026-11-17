using System.Text.Json;

namespace Dalan.Telegram;

/// <summary>TG-08: Telegram's reply_markup, turned into the reply keyboard of the core.</summary>
internal static class ReplyMarkup
{
    /// <summary>
    /// Returns null when all is well, otherwise what is wrong.
    /// <paramref name="keyboard"/>: null = the message does not change the keyboard, no rows = remove it.
    /// </summary>
    public static string? Read(TelegramParameters parameters, out List<List<string>>? keyboard)
    {
        keyboard = null;
        if (!parameters.TryObject("reply_markup", out var given)) return "can't parse reply markup: it must be a JSON object";
        if (given is not { } markup) return null;

        if (markup.TryGetProperty("remove_keyboard", out var remove) && remove.ValueKind == JsonValueKind.True)
        {
            keyboard = [];
            return null;
        }

        if (!markup.TryGetProperty("keyboard", out var rows))
            return "this kind of reply markup is not supported by Dalan; use keyboard or remove_keyboard";
        if (rows.ValueKind != JsonValueKind.Array) return "can't parse reply markup: keyboard must be a list of rows";

        keyboard = [];
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Array) return "can't parse reply markup: each keyboard row must be a list of buttons";

            var texts = new List<string>();
            foreach (var button in row.EnumerateArray())
            {
                if (ButtonText(button, out var text) is { } problem) return problem;
                texts.Add(text);
            }

            keyboard.Add(texts);
        }

        return null;
    }

    /// <summary>A button is a text, or an object with "text". A tap sends that text.</summary>
    private static string? ButtonText(JsonElement button, out string text)
    {
        text = "";
        if (button.ValueKind == JsonValueKind.String)
        {
            text = button.GetString()!;
            return null;
        }

        if (button.ValueKind != JsonValueKind.Object
            || !button.TryGetProperty("text", out var value) || value.ValueKind != JsonValueKind.String)
        {
            return "can't parse reply markup: a keyboard button must be a text or an object with text";
        }

        // A button that asks the user for a contact, a place or a web app would wait for an answer Dalan cannot give.
        foreach (var field in button.EnumerateObject())
        {
            if (field.Name.StartsWith("request_", StringComparison.Ordinal) || field.Name == "web_app")
                return $"keyboard button field {field.Name} is not supported by Dalan";
        }

        text = value.GetString()!;
        return null;
    }
}
