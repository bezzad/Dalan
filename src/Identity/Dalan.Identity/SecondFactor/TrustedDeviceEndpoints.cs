using System.Globalization;
using Dalan.Admin.Contracts;
using Dalan.Identity.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Identity.SecondFactor;

/// <summary>AUTH-44, AUTH-45: one trusted browser in the list. <see cref="Address"/> is null when the address is unknown.</summary>
internal sealed record TrustedDeviceJson(
    long Id, string Browser, string System, string? Address, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, bool Current);

internal sealed record TrustedDevicesResponse(IEnumerable<TrustedDeviceJson> Devices);

/// <summary>AUTH-44, AUTH-45: the browsers that skip the second factor, and forgetting one or all. Only with a session; each call writes the access log.</summary>
internal static class TrustedDeviceEndpoints
{
    public static void MapTrustedDevices(this IEndpointRouteBuilder endpoints)
    {
        var devices = endpoints.MapGroup("/api/account/trusted-devices").RequireAuthorization().AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            // ADM-01: this door knows its caller.
            await http.RequestServices.GetRequiredService<IAccessLog>().RecordAsync(AccessActor.User,
                SessionTokens.UserId(http.User), http.Connection.RemoteIpAddress, http.Connection.RemotePort, http.RequestAborted);
            return await next(context);
        });
        devices.MapGet("", ListAsync);
        devices.MapDelete("", ForgetAllAsync);
        devices.MapDelete("/{deviceId:long}", ForgetAsync);
    }

    private static async Task<IResult> ListAsync(HttpContext http, TrustedDevices trusted)
    {
        var current = TrustedDevices.TokenHash(http);
        var rows = await trusted.ListAsync(SessionTokens.UserId(http.User), http.RequestAborted);
        return AuthJson.Ok(new TrustedDevicesResponse(rows.Select(d => new TrustedDeviceJson(
            d.Id, d.Browser, d.Os, d.Address, d.CreatedAt, d.ExpiresAt, current is not null && d.TokenHash.AsSpan().SequenceEqual(current)))));
    }

    private static async Task<IResult> ForgetAsync(long deviceId, HttpContext http, TrustedDevices trusted, IAuditLog audit)
    {
        var userId = SessionTokens.UserId(http.User);
        if (!await trusted.ForgetAsync(userId, deviceId, http.RequestAborted))
            return AuthJson.Error(StatusCodes.Status404NotFound, "device_not_found", "You have no trusted device with this id.");

        await audit.RecordAsync(userId, "trusted_device.forget", string.Create(CultureInfo.InvariantCulture, $"device {deviceId}"), null,
            http.RequestAborted);
        return Results.NoContent();
    }

    private static async Task<IResult> ForgetAllAsync(HttpContext http, TrustedDevices trusted, IAuditLog audit)
    {
        var userId = SessionTokens.UserId(http.User);
        var count = await trusted.ForgetAllAsync(userId, http.RequestAborted);
        await audit.RecordAsync(userId, "trusted_device.forget_all", string.Create(CultureInfo.InvariantCulture, $"user {userId}"),
            count.ToString(CultureInfo.InvariantCulture), http.RequestAborted);
        return Results.NoContent();
    }
}
