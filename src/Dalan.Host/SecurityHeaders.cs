namespace Dalan.Host;

public static class SecurityHeaders
{
    /// <summary>
    /// SEC-04: the browser itself refuses anything that is not from our own origin
    /// (CLAUDE.md rule 7: the running product loads nothing from a foreign host).
    /// </summary>
    public static void UseSecurityHeaders(this WebApplication app) => app.Use((context, next) =>
    {
        var headers = context.Response.Headers;
        headers.ContentSecurityPolicy = "default-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "same-origin";
        return next(context);
    });
}
