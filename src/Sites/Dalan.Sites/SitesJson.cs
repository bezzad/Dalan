using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Dalan.Sites;

/// <summary>An error with a stable code and a message that says how to fix it.</summary>
internal sealed record ErrorJson(string Error, string Message);

/// <summary>The JSON of the Sites doors: snake_case names, like the other services.</summary>
internal static class SitesJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static IResult Ok<T>(T body) => Results.Json(body, Options);

    public static IResult Error(int status, string code, string message) =>
        Results.Json(new ErrorJson(code, message), Options, statusCode: status);

    /// <summary>Reads a JSON body. Null when the body is not JSON of this shape.</summary>
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
