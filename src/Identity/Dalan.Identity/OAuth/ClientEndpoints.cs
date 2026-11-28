using Dalan.Admin.Contracts;
using Dalan.Identity.Contracts;
using Dalan.Identity.Sessions;
using Dalan.Sites.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Dalan.Identity.OAuth;

internal sealed record TurnOnRequest(string? Site);

/// <summary>AUTH-40: a client in the builder's list. The secret is never here.</summary>
internal sealed record ClientJson(string ClientId, DateTimeOffset CreatedAt);

/// <summary>AUTH-40: the answer that carries the secret, once.</summary>
internal sealed record SecretJson(string ClientId, string Secret, DateTimeOffset CreatedAt);

internal sealed record ClientsJson(IEnumerable<ClientJson> Clients);

/// <summary>AUTH-43: a site the user allowed. <see cref="Url"/> is null when the site is gone.</summary>
internal sealed record AllowedSiteJson(string ClientId, string? Url, string[] Scopes, bool Email, DateTimeOffset CreatedAt);

internal sealed record AllowedSitesJson(IEnumerable<AllowedSiteJson> Sites);

/// <summary>
/// AUTH-40: the owner of a site (SITE-16, asked of Sites at each call) turns its client on and off and gets a new secret.
/// A session or an access key may call (like the other builder doors). AUTH-43: a user's own list of allowed sites
/// (session only). Every call writes the access log (ADM-01); every change of a client the audit log (ADM-09).
/// </summary>
internal static class ClientEndpoints
{
    public static void MapOAuthClients(this IEndpointRouteBuilder endpoints)
    {
        var clients = endpoints.MapGroup("/api/oauth/clients").RequireAuthorization(AccessKeys.SessionOrKeyPolicy).AddEndpointFilter(RecordAccess);
        clients.MapGet("", async (HttpContext http, OAuthStore store, ISiteDirectory sites) =>
        {
            var userId = SessionTokens.UserId(http.User);
            var mine = new List<ClientJson>();
            // A client whose site was deleted or has another owner now is off, so it is not listed.
            foreach (var client in await store.ClientsOfAsync(userId, http.RequestAborted))
                if ((await sites.FindAsync(client.ClientId, http.RequestAborted))?.OwnerUserId == userId)
                    mine.Add(new ClientJson(client.ClientId, client.CreatedAt));
            return AuthJson.Ok(new ClientsJson(mine));
        });
        clients.MapPost("", async (HttpContext http, OAuthStore store, ISiteDirectory sites, IAuditLog audit) =>
        {
            var userId = SessionTokens.UserId(http.User);
            var name = (await AuthJson.ReadAsync<TurnOnRequest>(http))?.Site?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(name) || (await sites.FindAsync(name, http.RequestAborted))?.OwnerUserId != userId) return NotYours();
            if (await store.TurnOnAsync(name, userId, http.RequestAborted) is not { } secret)
            {
                var client = await store.FindClientAsync(name, http.RequestAborted);
                return AuthJson.Ok(new ClientJson(name, client!.CreatedAt));
            }

            await audit.RecordAsync(userId, "oauth.on", $"site {name}", null, http.RequestAborted);
            var made = await store.FindClientAsync(name, http.RequestAborted);
            return Results.Json(new SecretJson(name, secret, made!.CreatedAt), AuthJson.Options, statusCode: StatusCodes.Status201Created);
        });
        clients.MapPost("/{clientId}/secret", async (string clientId, HttpContext http, OAuthStore store, ISiteDirectory sites, IAuditLog audit) =>
        {
            var userId = SessionTokens.UserId(http.User);
            if ((await sites.FindAsync(clientId, http.RequestAborted))?.OwnerUserId != userId
                || await store.NewSecretAsync(clientId, userId, http.RequestAborted) is not { } secret)
            {
                return NotYours();
            }

            await audit.RecordAsync(userId, "oauth.secret", $"site {clientId}", null, http.RequestAborted);
            var client = await store.FindClientAsync(clientId, http.RequestAborted);
            return AuthJson.Ok(new SecretJson(clientId, secret, client!.CreatedAt));
        });
        clients.MapDelete("/{clientId}", async (string clientId, HttpContext http, OAuthStore store, IAuditLog audit) =>
        {
            var userId = SessionTokens.UserId(http.User);
            if (!await store.TurnOffAsync(clientId, userId, http.RequestAborted)) return NotYours();
            await audit.RecordAsync(userId, "oauth.off", $"site {clientId}", null, http.RequestAborted);
            return Results.NoContent();
        });

        var allowed = endpoints.MapGroup("/api/account/allowed-sites").RequireAuthorization().AddEndpointFilter(RecordAccess);
        allowed.MapGet("", async (HttpContext http, OAuthStore store, ISiteDirectory sites) =>
        {
            var list = new List<AllowedSiteJson>();
            foreach (var consent in await store.ConsentsOfAsync(SessionTokens.UserId(http.User), http.RequestAborted))
            {
                var site = await sites.FindAsync(consent.ClientId, http.RequestAborted);
                list.Add(new AllowedSiteJson(consent.ClientId, site?.Url, consent.Scopes.Split(' '), consent.Email, consent.CreatedAt));
            }

            return AuthJson.Ok(new AllowedSitesJson(list));
        });
        allowed.MapDelete("/{clientId}", async (string clientId, HttpContext http, OAuthStore store) =>
            await store.RemoveConsentAsync(SessionTokens.UserId(http.User), clientId, http.RequestAborted)
                ? Results.NoContent()
                : AuthJson.Error(StatusCodes.Status404NotFound, "not_found", "This site is not in your list."));
    }

    private static IResult NotYours() => AuthJson.Error(StatusCodes.Status404NotFound, "site_not_found",
        "You have no site with this name, or Login with Dalan is not on for it. Reserve the name in My apps first.");

    private static async ValueTask<object?> RecordAccess(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        await http.RequestServices.GetRequiredService<IAccessLog>().RecordAsync(AccessActor.User,
            SessionTokens.UserId(http.User), http.Connection.RemoteIpAddress, http.Connection.RemotePort, http.RequestAborted);
        return await next(context);
    }
}
