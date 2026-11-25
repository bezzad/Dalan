using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Dalan.Relay;

/// <summary>REL-11: every error has a stable code and a message that says how to fix it.</summary>
internal sealed record ErrorJson(
    string Error,
    string Message,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] long? ToUserId = null);

/// <summary>The JSON style of both doors of Relay (Bot API and Client API): snake_case names.</summary>
internal static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        // Persian text stays readable and short in the JSON (the default would write \uXXXX for every letter).
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static IResult Ok<T>(T body) => Results.Json(body, Options);

    /// <summary><paramref name="toUserId"/>: only for chat_moved (REL-56).</summary>
    public static IResult Error(int status, string code, string message, long? toUserId = null) =>
        Results.Json(new ErrorJson(code, message, toUserId), Options, statusCode: status);

    /// <summary>REL-38.</summary>
    public static IResult BotSuspended() => Error(StatusCodes.Status403Forbidden, "bot_suspended",
        "This bot is suspended by the admin of Dalan.");

    public static IResult BadRequest(string message) => Error(StatusCodes.Status400BadRequest, "bad_request", message);

    public static IResult TooManyRequests(HttpContext http, TimeSpan wait)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds));
        http.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        return Error(StatusCodes.Status429TooManyRequests, "rate_limited",
            $"Too many calls. Wait {seconds} second(s) and try again.");
    }

    /// <summary>Reads a JSON body. Returns null when the body is not JSON of this shape or has another content type (SEC-05).</summary>
    public static async Task<T?> ReadAsync<T>(HttpContext http) where T : class
    {
        try
        {
            return await http.Request.ReadFromJsonAsync<T>(Options, http.RequestAborted);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Reads a whole number, 0 or more, from the query. False and an error text when it is not one.</summary>
    public static bool TryReadNumber(IQueryCollection query, string name, out long? value, out string error)
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
