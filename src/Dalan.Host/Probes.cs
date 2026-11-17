using Microsoft.AspNetCore.RateLimiting;

namespace Dalan.Host;

/// <summary>OPS-01 and OPS-02: liveness and readiness probes. Not public (OPS-06, see deploy/Caddyfile).</summary>
public static class Probes
{
    private const string Health = "/health";
    private const string Ready = "/ready";

    public static void MapProbes(this WebApplication app)
    {
        var version = app.Configuration["Dalan:Version"] ?? "dev";
        var stopping = app.Lifetime.ApplicationStopping;
        var startup = app.Services.GetRequiredService<Startup>();

        app.MapGet(Health, () => Results.Ok(new { status = "ok", version })).DisableRateLimiting();

        app.MapGet(Ready, () => startup.Done.IsCompletedSuccessfully && !stopping.IsCancellationRequested
            ? Results.Ok(new { status = "ready" })
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable)).DisableRateLimiting();
    }

    /// <summary>OPS-08: until the start-up work is done, only the probes are served.</summary>
    public static void UseReadyGate(this WebApplication app)
    {
        var startup = app.Services.GetRequiredService<Startup>();

        app.Use(async (context, next) =>
        {
            if (startup.Done.IsCompletedSuccessfully || context.Request.Path == Health || context.Request.Path == Ready)
            {
                await next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "not_ready",
                message = "The server is starting. Try again in a few seconds.",
            });
        });
    }
}
