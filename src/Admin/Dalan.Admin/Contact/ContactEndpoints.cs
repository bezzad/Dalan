using System.Globalization;
using System.Security.Claims;
using Dalan.Admin.AdminApi;
using Dalan.Admin.Contracts;
using Dalan.Admin.Data;
using Dalan.Identity.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dalan.Admin.Contact;

internal sealed record ContactRequest(string? Email, string? Text);

internal sealed record ReadRequest(bool? Read);

internal sealed record ContactJson(long Id, long? UserId, string Email, string Text, string Address, int? Port, DateTimeOffset At, bool Read);

/// <summary>ADM-17: the contact page sends here with or without a session; only an admin reads.</summary>
internal static class ContactEndpoints
{
    public static void MapContact(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/contact", SendAsync);
        var admin = endpoints.MapGroup("/api/admin/contact").RequireAuthorization().AddEndpointFilter<AdminFilter>();
        admin.MapGet("", ListAsync);
        admin.MapPost("/{id:long}/read", MarkAsync);
    }

    private static async Task<IResult> SendAsync(
        HttpContext http, IDbContextFactory<AdminDb> dbFactory, IUserAdmin users, IAccessLog accessLog,
        IOptionsMonitor<AdminOptions> options, TimeProvider time)
    {
        var request = await AdminJson.ReadAsync<ContactRequest>(http);
        var address = http.Connection.RemoteIpAddress;
        var port = http.Connection.RemotePort;
        var limits = options.CurrentValue;

        // A logged-in user who has an email always answers there, whatever the page sent.
        long? userId = http.User.FindFirstValue("sub") is { } sub ? long.Parse(sub, CultureInfo.InvariantCulture) : null;
        string? email = null;
        if (userId is { } id)
        {
            await accessLog.RecordAsync(AccessActor.User, id, address, port, http.RequestAborted);
            email = (await users.FindAsync(id, http.RequestAborted))?.Email;
        }
        email ??= users.NormalizeEmail(request?.Email);
        if (email is null)
            return AdminJson.Error(StatusCodes.Status400BadRequest, "bad_email", "Send a valid email address, so we can answer.");

        var text = request?.Text?.Trim() ?? "";
        if (text.Length == 0 || text.Length > limits.ContactMaxLength)
            return AdminJson.Error(StatusCodes.Status400BadRequest, "bad_text",
                $"The text must have 1 to {limits.ContactMaxLength} characters.");
        if (address is null) return AdminJson.BadRequest("The address of the connection is unknown.");
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();

        var now = time.GetUtcNow();
        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var since = now.AddDays(-1);
        if (await db.ContactMessages.CountAsync(m => m.Address == address && m.At > since, http.RequestAborted) >= limits.ContactPerAddressPerDay)
            return AdminJson.Error(StatusCodes.Status429TooManyRequests, "too_many_messages",
                "This address sent too many messages today. Try again tomorrow.");

        var message = new ContactMessage { UserId = userId, Email = email, Text = text, Address = address, Port = port, At = now };
        db.ContactMessages.Add(message);
        await db.SaveChangesAsync(http.RequestAborted);
        return Results.Json(new { message.Id }, AdminJson.Options, statusCode: StatusCodes.Status201Created);
    }

    /// <summary>Newest first; "before" gives the next page.</summary>
    private static async Task<IResult> ListAsync(HttpContext http, IDbContextFactory<AdminDb> dbFactory, IOptionsMonitor<AdminOptions> options)
    {
        var text = http.Request.Query["before"].ToString();
        long? before = null;
        if (text.Length > 0)
        {
            if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                return AdminJson.BadRequest("\"before\" must be a message id.");
            before = number;
        }

        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        var rows = await db.ContactMessages.AsNoTracking()
            .Where(m => before == null || m.Id < before)
            .OrderByDescending(m => m.Id)
            .Take(options.CurrentValue.PageSize)
            .ToListAsync(http.RequestAborted);
        return AdminJson.Ok(new RowsResponse<ContactJson>(rows.Select(Json)));
    }

    private static async Task<IResult> MarkAsync(long id, HttpContext http, IDbContextFactory<AdminDb> dbFactory, IAuditLog audit)
    {
        if (await AdminJson.ReadAsync<ReadRequest>(http) is not { Read: { } read })
            return AdminJson.BadRequest("""Send JSON like {"read": true}.""");

        await using var db = await dbFactory.CreateDbContextAsync(http.RequestAborted);
        if (await db.ContactMessages.FindAsync([id], http.RequestAborted) is not { } message)
            return AdminJson.NotFound("message_not_found", "There is no message with this id.");
        message.Read = read;
        await db.SaveChangesAsync(http.RequestAborted);
        await audit.RecordAsync(AdminFilter.UserId(http), read ? "contact.read" : "contact.unread",
            string.Create(CultureInfo.InvariantCulture, $"message {id}"), null, http.RequestAborted);
        return AdminJson.Ok(Json(message));
    }

    private static ContactJson Json(ContactMessage m) =>
        new(m.Id, m.UserId, m.Email, m.Text, m.Address.ToString(), m.Port, m.At, m.Read);
}
