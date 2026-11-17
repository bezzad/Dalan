using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace Dalan.Admin.AdminApi;

internal sealed record ErrorJson(string Error, string Message);

/// <summary>The JSON style of the admin door: snake_case names and enum values, errors with a stable code, like Relay.</summary>
internal static class AdminJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public static IResult Ok<T>(T body) => Results.Json(body, Options);

    public static IResult Error(int status, string code, string message) =>
        Results.Json(new ErrorJson(code, message), Options, statusCode: status);

    public static IResult BadRequest(string message) => Error(StatusCodes.Status400BadRequest, "bad_request", message);

    public static IResult NotFound(string code, string message) => Error(StatusCodes.Status404NotFound, code, message);

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
}
