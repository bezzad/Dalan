using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dalan.Testing;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Dalan.Identity.Tests;

/// <summary>Short helpers for Identity tests: calls without a cookie jar, so a test chooses which cookie it sends.</summary>
internal static class Http
{
    public const string SessionCookie = "__Host-dalan_session";
    public const string RefreshCookie = "__Host-dalan_refresh";

    public static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public static async Task<DalanApp> StartAsync(PostgresServer postgres, params (string Key, string Value)[] settings)
    {
        var app = new DalanApp(await postgres.NewDatabaseAsync(), settings.ToDictionary(s => s.Key, s => s.Value));
        await app.ReadyAsync();
        return app;
    }

    public static async Task<string?> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Cancel)).GetProperty("error").GetString();

    public static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Cancel);

    /// <summary>The whole Set-Cookie header of one cookie, or null when the response does not set it.</summary>
    public static string? SetCookie(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.SingleOrDefault(cookie => cookie.StartsWith($"{name}=", StringComparison.Ordinal))
            : null;

    public static string CookieValue(HttpResponseMessage response, string name) =>
        SetCookie(response, name)!.Split(';')[0][(name.Length + 1)..];

    /// <summary>Logs in with the development login, with no cookie jar. Returns the two tokens.</summary>
    public static async Task<(string Session, string Refresh)> LoginRawAsync(DalanApp app, string phone = "09121234567")
    {
        var response = await NoJar(app).PostAsJsonAsync("/api/dev/login", new { phone, name = "Sara" }, Cancel);
        response.EnsureSuccessStatusCode();
        return (CookieValue(response, SessionCookie), CookieValue(response, RefreshCookie));
    }

    public static HttpClient NoJar(DalanApp app) => app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    /// <summary>A call that carries exactly the cookies given.</summary>
    public static async Task<HttpResponseMessage> SendAsync(
        DalanApp app, HttpMethod method, string path, string? session = null, string? refresh = null)
    {
        using var request = new HttpRequestMessage(method, path);
        var cookies = new List<string>();
        if (session is not null) cookies.Add($"{SessionCookie}={session}");
        if (refresh is not null) cookies.Add($"{RefreshCookie}={refresh}");
        if (cookies.Count > 0) request.Headers.Add("Cookie", string.Join("; ", cookies));
        return await NoJar(app).SendAsync(request, Cancel);
    }

    public static Task<HttpResponseMessage> RefreshAsync(DalanApp app, string? refresh) =>
        SendAsync(app, HttpMethod.Post, "/api/auth/refresh", refresh: refresh);

    public static string Sha256Hex(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
