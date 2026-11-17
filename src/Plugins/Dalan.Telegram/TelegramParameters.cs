using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Dalan.Telegram;

/// <summary>TG-03: the parameters of one call, from the query string, a form body or a JSON body.</summary>
internal sealed class TelegramParameters
{
    private readonly Dictionary<string, JsonElement> _values = new(StringComparer.Ordinal);

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
                foreach (var (name, value) in await request.ReadFormAsync(http.RequestAborted))
                    parameters.Set(name, value.ToString());
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

    /// <summary>
    /// A JSON object, sent as an object or as a string of JSON (the only way in a form).
    /// False when the parameter is there but is not a JSON object.
    /// </summary>
    public bool TryObject(string name, out JsonElement? value)
    {
        value = null;
        if (!_values.TryGetValue(name, out var raw)) return true;

        if (raw.ValueKind == JsonValueKind.String)
        {
            try
            {
                raw = JsonSerializer.Deserialize<JsonElement>(raw.GetString()!);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        value = raw;
        return raw.ValueKind == JsonValueKind.Object;
    }

    private void Set(string name, string value) => _values[name] = JsonSerializer.SerializeToElement(value);
}
