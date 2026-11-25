using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Dalan.Identity;

/// <summary>What a person may see of an account. Never the phone number or the password hash (AUTH-04).</summary>
internal sealed record ProfileJson(
    long Id, string Name, string? Username, string? Email, bool HasPassword, bool HasPhone, bool Admin, string Builder, bool HasGithub);

internal sealed record ErrorJson(string Error, string Message);

/// <summary>The JSON style of the Identity endpoints: snake_case names, errors with a stable code, like Relay.</summary>
internal static class AuthJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        // Persian names stay readable in the JSON.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static IResult Ok<T>(T body) => Results.Json(body, Options);

    public static IResult Error(int status, string code, string message) =>
        Results.Json(new ErrorJson(code, message), Options, statusCode: status);

    public static IResult BadRequest(string code, string message) => Error(StatusCodes.Status400BadRequest, code, message);

    public static IResult Unauthorized() => Error(StatusCodes.Status401Unauthorized, "unauthorized",
        "You are not logged in, or the session has ended. Log in again.");

    /// <summary>SEC-06.</summary>
    public static IResult BadProof() => BadRequest("bad_proof",
        "The proof of work is missing, wrong or too old. Get a new challenge from POST /api/auth/challenge and solve it.");

    /// <summary>AUTH-20.</summary>
    public static IResult Suspended() => Error(StatusCodes.Status403Forbidden, "suspended",
        "This account is suspended by the admin of Dalan.");

    public static IResult TooManyRequests(HttpContext http, string code, TimeSpan wait, string message)
    {
        http.Response.Headers.RetryAfter = Seconds(wait).ToString(CultureInfo.InvariantCulture);
        return Error(StatusCodes.Status429TooManyRequests, code, message);
    }

    public static int Seconds(TimeSpan wait) => Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds));

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
}
