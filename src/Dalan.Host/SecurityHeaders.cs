using Dalan.Relay.Contracts;

namespace Dalan.Host;

public static class SecurityHeaders
{
    /// <summary>
    /// SEC-04: the browser itself refuses anything that is not from our own origin
    /// (CLAUDE.md rule 7: the running product loads nothing from a foreign host).
    /// SEC-16: the only frame allowed is the file view host, when the viewer is on.
    /// </summary>
    public static void UseSecurityHeaders(this WebApplication app)
    {
        var view = app.Services.GetRequiredService<IFileViewHost>();
        app.Use((context, next) =>
        {
            var headers = context.Response.Headers;
            const string policy = "default-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
            headers.ContentSecurityPolicy = view.Origin is { } origin ? $"{policy}; frame-src {origin}" : policy;
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "same-origin";
            return next(context);
        });
    }
}
