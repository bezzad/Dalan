using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Dalan.Admin.Contracts;
using Dalan.Notification.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dalan.Notification.Inbox;

internal sealed record NoticeJson(long Id, string Title, string Text, string? Link, DateTimeOffset At, bool Read);

internal sealed record InboxJson(IEnumerable<NoticeJson> Notices, int Unread);

internal sealed record ReadRequest(long? UpTo);

internal sealed record ErrorJson(string Error, string Message);

/// <summary>NTF-12: the user's own inbox. Only with a session; each call writes the access log (ADM-01).</summary>
internal static class InboxEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static void MapInbox(this IEndpointRouteBuilder endpoints)
    {
        var inbox = endpoints.MapGroup("/api/inbox").RequireAuthorization().AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            await http.RequestServices.GetRequiredService<IAccessLog>().RecordAsync(AccessActor.User,
                UserId(http), http.Connection.RemoteIpAddress, http.Connection.RemotePort, http.RequestAborted);
            return await next(context);
        });
        inbox.MapGet("", ListAsync);
        inbox.MapPost("/read", ReadAsync);
    }

    /// <summary>"sub" is the standard JWT claim; Identity puts the user id there.</summary>
    private static long UserId(HttpContext http) => long.Parse(http.User.FindFirstValue("sub")!, CultureInfo.InvariantCulture);

    /// <summary>Newest first; <paramref name="before"/> gives the next page.</summary>
    private static async Task<IResult> ListAsync(
        long? before, HttpContext http, IDbContextFactory<NotificationDb> dbFactory, IOptionsMonitor<NotificationOptions> options)
    {
        var userId = UserId(http);
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var mine = db.Inbox.AsNoTracking().Where(n => n.UserId == userId);
        var page = await mine.Where(n => before == null || n.Id < before)
            .OrderByDescending(n => n.Id)
            .Take(options.CurrentValue.InboxPageSize)
            .Select(n => new NoticeJson(n.Id, n.Title, n.Text, n.Link, n.CreatedAt, n.Read))
            .ToListAsync(http.RequestAborted);
        var unread = await mine.CountAsync(n => !n.Read, http.RequestAborted);
        return Results.Json(new InboxJson(page, unread), Json);
    }

    /// <summary>Marks the user's notices up to this id read.</summary>
    private static async Task<IResult> ReadAsync(HttpContext http, IDbContextFactory<NotificationDb> dbFactory)
    {
        ReadRequest? body;
        try
        {
            body = await http.Request.ReadFromJsonAsync<ReadRequest>(Json, http.RequestAborted);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            body = null;
        }

        if (body?.UpTo is not { } upTo)
        {
            return Results.Json(new ErrorJson("bad_request", """Send JSON like {"up_to": 12} with Content-Type application/json."""),
                Json, statusCode: StatusCodes.Status400BadRequest);
        }

        var userId = UserId(http);
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        await db.Inbox.Where(n => n.UserId == userId && n.Id <= upTo && !n.Read)
            .ExecuteUpdateAsync(set => set.SetProperty(n => n.Read, true), http.RequestAborted);
        return Results.NoContent();
    }
}
