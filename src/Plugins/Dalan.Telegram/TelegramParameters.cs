using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Dalan.Telegram;

/// <summary>TG-03: the parameters of one call, from the query string, a form body or a JSON body.</summary>
internal sealed class TelegramParameters
{
    private readonly Dictionary<string, JsonElement> _values = new(StringComparer.Ordinal);
    private IFormFileCollection? _files;

    /// <summary>Returns null when the body cannot be read.</summary>
    public static async Task<TelegramParameters?> ReadAsync(HttpContext http)
    {
        var parameters = new TelegramParameters();
        var request = http.Request;
        foreach (var (name, value) in request.Query) parameters.Set(name, value.ToString());

        try
        {
            if (request.HasFormContentType)
            {
                var form = await request.ReadFormAsync(http.RequestAborted);
                foreach (var (name, value) in form) parameters.Set(name, value.ToString());
                parameters._files = form.Files;
            }
            else if (request.HasJsonContentType() && request.ContentLength != 0)
            {
                var body = await request.ReadFromJsonAsync<JsonElement>(http.RequestAborted);
                if (body.ValueKind != JsonValueKind.Object) return null;
                foreach (var property in body.EnumerateObject()) parameters._values[property.Name] = property.Value;
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            return null;
        }

        return parameters;
    }

    /// <summary>A whole number, sent as a number or as a string. Null when it is missing or not one.</summary>
    public long? Number(string name) => _values.GetValueOrDefault(name) switch
    {
        { ValueKind: JsonValueKind.Number } value when value.TryGetInt64(out var number) => number,
        { ValueKind: JsonValueKind.String } value
            when long.TryParse(value.GetString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) => number,
        _ => null,
    };

    public string? Text(string name) => _values.GetValueOrDefault(name) switch
    {
        { ValueKind: JsonValueKind.String } value => value.GetString(),
        { ValueKind: JsonValueKind.Number } value => value.GetRawText(),
        _ => null,
    };

    /// <summary>True for JSON true or the text "true", as forms send it.</summary>
    public bool Flag(string name) => _values.GetValueOrDefault(name) switch
    {
        { ValueKind: JsonValueKind.True } => true,
        { ValueKind: JsonValueKind.String } value => string.Equals(value.GetString(), "true", StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    public bool Has(string name) => _values.ContainsKey(name);

    /// <summary>A file part of a multipart body (TG-20).</summary>
    public IFormFile? File(string name) => _files?.GetFile(name);

    /// <summary>A JSON list, sent as a list or as a string of JSON. False when the parameter is there but is not a list.</summary>
    public bool TryArray(string name, out JsonElement? value)
    {
        value = null;
        if (!_values.TryGetValue(name, out var raw)) return true;
        if (!TryParse(ref raw)) return false;

        value = raw;
        return raw.ValueKind == JsonValueKind.Array;
    }

    /// <summary>
    /// A JSON object, sent as an object or as a string of JSON (the only way in a form).
    /// False when the parameter is there but is not a JSON object.
    /// </summary>
    public bool TryObject(string name, out JsonElement? value)
    {
        value = null;
        if (!_values.TryGetValue(name, out var raw)) return true;

        if (!TryParse(ref raw)) return false;

        value = raw;
        return raw.ValueKind == JsonValueKind.Object;
    }

    /// <summary>A string holds JSON text in a form or a query; parse it.</summary>
    private static bool TryParse(ref JsonElement raw)
    {
        if (raw.ValueKind != JsonValueKind.String) return true;

        try
        {
            raw = JsonSerializer.Deserialize<JsonElement>(raw.GetString()!);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private void Set(string name, string value) => _values[name] = JsonSerializer.SerializeToElement(value);
}
