using Dalan.Notification.Contracts;
using Dalan.Relay.ClientApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Dalan.Relay.Push;

internal sealed record PushKeyJson(string? Key);

internal sealed record SubscriptionKeys(string? P256dh, string? Auth);

/// <summary>The browser's own subscription JSON (PushSubscription.toJSON()).</summary>
internal sealed record SubscribeRequest(string? Endpoint, SubscriptionKeys? Keys);

internal sealed record UnsubscribeRequest(string? Endpoint);

/// <summary>
/// REL-35: the door to Web Push. It is in the Client API group, so the session, the access log and the limit per user hold.
/// The subscriptions themselves live in Notification.
/// </summary>
internal static class PushEndpoints
{
    public static void MapPush(this RouteGroupBuilder api)
    {
        api.MapGet("/push", (IWebPush push) => ApiJson.Ok(new PushKeyJson(push.PublicKey)));
        api.MapPost("/push/subscribe", SubscribeAsync);
        api.MapPost("/push/unsubscribe", UnsubscribeAsync);
    }

    private static async Task<IResult> SubscribeAsync(HttpContext http, IWebPush push)
    {
        if (await ApiJson.ReadAsync<SubscribeRequest>(http) is not { Endpoint: { } endpoint, Keys: { P256dh: { } p256dh, Auth: { } auth } })
            return BadSubscription();

        return await push.SubscribeAsync(ClientApiFilter.UserId(http), new PushDevice(endpoint, p256dh, auth), http.RequestAborted) switch
        {
            SubscribeResult.Saved => Results.NoContent(),
            SubscribeResult.PushOff => ApiJson.Error(StatusCodes.Status503ServiceUnavailable, "push_off",
                "Notifications are not set up on this server."),
            _ => BadSubscription(),
        };
    }

    private static async Task<IResult> UnsubscribeAsync(HttpContext http, IWebPush push)
    {
        if (await ApiJson.ReadAsync<UnsubscribeRequest>(http) is not { Endpoint: { } endpoint })
            return ApiJson.BadRequest("""Send JSON like {"endpoint": "https://..."} with Content-Type application/json.""");

        await push.UnsubscribeAsync(ClientApiFilter.UserId(http), endpoint, http.RequestAborted);
        return Results.NoContent();
    }

    private static IResult BadSubscription() => ApiJson.Error(StatusCodes.Status400BadRequest, "bad_subscription",
        "Send the browser's push subscription as JSON: endpoint (of a known push service) and keys.p256dh and keys.auth.");
}
