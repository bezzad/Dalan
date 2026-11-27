using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Dalan.Identity.Contracts;
using Dalan.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Admin.Tests;

/// <summary>SEC-14: the server is the gate. The calls are read from the route table, so a new endpoint is covered at once.</summary>
public partial class AdminGateTests(PostgresServer postgres)
{
    private static CancellationToken Cancel => AdminApp.Cancel;

    /// <summary>Many calls in a row from one test address: the limits must not answer first.</summary>
    private static readonly (string, string)[] NoLimits =
        [("Dalan:IpRequestLimit", "100000"), ("Relay:UserRequestLimit", "100000"), ("Relay:BotRequestLimit", "100000")];

    private sealed record Call(string Method, string Pattern);

    /// <summary>Every API call of the route table: method and route pattern. Catch-all routes (the page itself) are left out.</summary>
    private static List<Call> Calls(DalanApp app) =>
    [
        .. app.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText is { } raw && !raw.Contains('*', StringComparison.Ordinal))
            .SelectMany(e => (e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? []).Select(m => new Call(m, Normalize(e.RoutePattern.RawText!))))
            .Distinct(),
    ];

    private static string Normalize(string raw) => raw.StartsWith('/') ? raw : "/" + raw;

    private static bool IsAdmin(Call call) => call.Pattern.StartsWith("/api/admin", StringComparison.Ordinal);

    /// <summary>The pattern with each {parameter} filled in by <paramref name="value"/>.</summary>
    private static string Path(string pattern, Func<string, string> value) =>
        Parameter().Replace(pattern, m => value(m.Groups[1].Value));

    [GeneratedRegex(@"\{([A-Za-z]+)[^}]*\}")]
    private static partial Regex Parameter();

    private static HttpRequestMessage Request(Call call, string path, object? body = null)
    {
        var request = new HttpRequestMessage(new HttpMethod(call.Method), path);
        if (body is not null && call.Method is not ("GET" or "HEAD" or "DELETE")) request.Content = JsonContent.Create(body);
        return request;
    }

    /// <summary>Sends and reads the answer, or null when it does not end in time (a live stream or a long poll).</summary>
    private static async Task<(HttpStatusCode Status, string Text)?> TrySendAsync(HttpClient client, HttpRequestMessage request)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        limit.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, limit.Token);
            return (response.StatusCode, await response.Content.ReadAsStringAsync(limit.Token));
        }
        catch (OperationCanceledException) when (!Cancel.IsCancellationRequested)
        {
            return null;
        }
    }

    [Fact]
    [Trait("Rule", "SEC-14")]
    public async Task SEC14_every_admin_call_refuses_a_common_user_the_same_way_and_is_logged()
    {
        await using var app = await AdminApp.StartAsync(postgres, NoLimits);
        var (user, userId) = await app.LoginAsync("Sara");
        var calls = Calls(app).Where(IsAdmin).ToList();
        Assert.Contains(new Call("GET", "/api/admin/settings"), calls);
        Assert.Contains(new Call("GET", "/api/admin/overview"), calls);

        var answers = new List<string>();
        for (var i = 0; i < calls.Count; i++)
        {
            var request = Request(calls[i], Path(calls[i].Pattern, _ => "1"), new { value = "1", approve = true, suspended = true, read = true });
            request.Headers.Add("Test-Socket", $"203.0.113.{i + 1}:{40000 + i}");
            using var response = await user.SendAsync(request, Cancel);

            Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{calls[i]}: {(int)response.StatusCode}");
            answers.Add(await response.Content.ReadAsStringAsync(Cancel));
        }

        Assert.Single(answers.Distinct());
        Assert.Contains("not_admin", answers[0], StringComparison.Ordinal);
        var log = await app.AccessLogAsync();
        for (var i = 0; i < calls.Count; i++)
            Assert.True(log.Contains($"User {userId} 203.0.113.{i + 1} {40000 + i}"), $"{calls[i]} is not in the access log");
        Assert.Equal(0, await PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT count(*) FROM admin.settings"));
        Assert.Equal(0, await PostgresServer.ScalarAsync<long>(app.ConnectionString, "SELECT count(*) FROM admin.audit_log"));
    }

    [Fact]
    [Trait("Rule", "SEC-14")]
    public async Task SEC14_no_api_makes_a_user_an_admin()
    {
        await using var app = await AdminApp.StartAsync(postgres, NoLimits);
        var (admin, _) = await app.AdminAsync();
        var (user, userId) = await app.LoginAsync("Sara");
        var body = new
        {
            phone = AdminApp.AdminPhone, admin = true, is_admin = true, role = "admin", key = "Identity:AdminPhones",
            value = AdminApp.AdminPhone, admin_phones = AdminApp.AdminPhone,
        };

        // Every call that changes something, with every way of asking to be an admin in its body.
        foreach (var call in Calls(app).Where(c => c.Method is not ("GET" or "HEAD")
                     && c.Pattern is not ("/api/dev/login" or "/api/logout")))
            await TrySendAsync(user, Request(call, Path(call.Pattern, name => name == "userId" ? userId.ToString(System.Globalization.CultureInfo.InvariantCulture) : "1"), body));

        // The admin's own panel cannot read or change the list of admins either.
        var put = await admin.SetAsync("Identity:AdminPhones", "09121111111");
        var settings = await admin.GetStringAsync("/api/admin/settings", Cancel);

        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
        Assert.DoesNotContain("AdminPhones", settings, StringComparison.Ordinal);
        Assert.False((await app.Services.GetRequiredService<IUserDirectory>().GetAccessAsync(userId, Cancel)).IsAdmin);
        Assert.False((await user.JsonAsync("/api/me")).GetProperty("admin").GetBoolean());
        Assert.Equal(AdminApp.AdminPhone, app.Services.GetRequiredService<IConfiguration>()["Identity:AdminPhones"]);
        Assert.Equal(0, await PostgresServer.ScalarAsync<long>(app.ConnectionString,
            "SELECT count(*) FROM admin.settings WHERE key ILIKE '%AdminPhones%'"));
    }

    [Fact]
    [Trait("Rule", "SEC-14")]
    public async Task SEC14_common_answers_hold_no_admin_data()
    {
        await using var app = await AdminApp.StartAsync(postgres, NoLimits);
        var (admin, _) = await app.AdminAsync();
        DalanApp.From(admin, "198.51.100.77:41000");
        // A setting value, an audit row and an access log row that only an admin may see.
        (await admin.SetAsync("Notification:Push:AllowedHosts", "leak-check.example")).EnsureSuccessStatusCode();
        await admin.JsonAsync("/api/admin/audit");
        // Another user with a phone, an email, a bot, and a support message.
        var other = await app.AddUserAsync("Other", "other_user", "+989120005555");
        await PostgresServer.ExecuteAsync(app.ConnectionString, $"UPDATE identity.users SET email = 'other-secret@example.com' WHERE id = {other}");
        var (botId, _) = await app.AddBotAsync("other_bot", other);
        (await DalanApp.From(app.Browser(), "203.0.113.9:40000").PostAsJsonAsync("/api/contact", new { email = "sender-secret@example.com", text = "Hello" }, Cancel))
            .EnsureSuccessStatusCode();
        var (user, _) = await app.LoginAsync("Sara");
        string[] secrets =
        [
            "9129990001", "9120005555", "other-secret@example.com", "sender-secret@example.com", "leak-check.example",
            "198.51.100.77", "setting.set",
        ];

        var checkedCalls = 0;
        foreach (var call in Calls(app).Where(c => c.Method == "GET" && !IsAdmin(c)))
        {
            var path = Path(call.Pattern, name => name switch
            {
                "userId" => other.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "botId" => botId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "username" => "other_bot",
                _ => "1",
            });
            if (await TrySendAsync(user, Request(call, path)) is not { } answer) continue;
            checkedCalls++;
            foreach (var secret in secrets)
                Assert.False(answer.Text.Contains(secret, StringComparison.OrdinalIgnoreCase), $"{call} ({path}) shows {secret}");
        }

        Assert.True(checkedCalls > 10, $"only {checkedCalls} calls answered");
    }
}
