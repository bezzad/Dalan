namespace Dalan.Host;

/// <summary>OPS-01 and OPS-02: liveness and readiness probes. Not public (OPS-06, see deploy/Caddyfile).</summary>
public static class Probes
{
    public static void MapProbes(this WebApplication app)
    {
        var version = app.Configuration["Dalan:Version"] ?? "dev";
        var stopping = app.Lifetime.ApplicationStopping;

        app.MapGet("/health", () => Results.Ok(new { status = "ok", version }));

        app.MapGet("/ready", () => stopping.IsCancellationRequested
            ? Results.StatusCode(StatusCodes.Status503ServiceUnavailable)
            : Results.Ok(new { status = "ready" }));
    }
}
