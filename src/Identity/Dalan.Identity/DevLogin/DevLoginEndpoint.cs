using System.Text.Json;
using Dalan.Identity.Data;
using Dalan.Identity.Sessions;
using Dalan.Identity.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dalan.Identity.DevLogin;

/// <summary>
/// AUTH-06: login by phone number with no SMS, for development only. Real login is milestone 4.
/// It needs two switches: the setting Identity:DevLogin and the Development environment.
/// </summary>
internal static class DevLoginEndpoint
{
    private sealed record Request(string? Phone, string? Name);

    public static void MapDevLogin(this IEndpointRouteBuilder endpoints)
    {
        if (!endpoints.ServiceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value.DevLogin) return;

        if (!endpoints.ServiceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            throw new InvalidOperationException(
                "Identity:DevLogin is on, but this is not the Development environment. " +
                "Development login lets anyone log in as any phone number. Turn the setting off.");
        }

        endpoints.MapPost("/api/dev/login", LoginAsync);
    }

    private static async Task<IResult> LoginAsync(
        HttpContext http,
        IDbContextFactory<IdentityDb> dbFactory,
        SessionTokens sessions,
        IOptionsMonitor<IdentityOptions> options,
        TimeProvider time)
    {
        Request? request;
        try
        {
            request = await http.Request.ReadFromJsonAsync<Request>(http.RequestAborted);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            request = null;
        }

        var phone = PhoneNumber.Normalize(request?.Phone);
        var name = request?.Name?.Trim() ?? "";
        if (phone is null || name.Length == 0 || name.Length > options.CurrentValue.MaxNameLength)
        {
            return Results.Json(
                new
                {
                    error = "bad_request",
                    message = """Send JSON like {"phone": "09121234567", "name": "Sara"}: an Iranian mobile number and a short name.""",
                },
                statusCode: StatusCodes.Status400BadRequest);
        }

        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var now = time.GetUtcNow();
        var ids = await db.Database.SqlQuery<long>($"""
            INSERT INTO identity.users (phone, display_name, created_at)
            VALUES ({phone}, {name}, {now})
            ON CONFLICT (phone) DO UPDATE SET display_name = EXCLUDED.display_name
            RETURNING id AS "Value"
            """).ToListAsync(http.RequestAborted);

        sessions.SignIn(http, ids[0]);
        return Results.Json(new ProfileJson(ids[0], name, Username: null));
    }
}
