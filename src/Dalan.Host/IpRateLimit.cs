using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Dalan.Host;

/// <summary>
/// SEC-02: at most N calls per window for each client address, on every endpoint of every service,
/// counted before any token or session is checked. Limits per bot and per user are in Relay.
/// </summary>
public static class IpRateLimit
{
    public static IServiceCollection AddIpRateLimit(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddRateLimiter(limiter =>
        {
            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
                RateLimitPartition.GetFixedWindowLimiter(
                    http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = configuration.GetValue<int>("Dalan:IpRequestLimit"),
                        Window = TimeSpan.FromSeconds(configuration.GetValue<int>("Dalan:IpLimitWindowSeconds")),
                        QueueLimit = 0,
                    }));

            limiter.OnRejected = async (context, cancellationToken) =>
            {
                var wait = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? retryAfter
                    : TimeSpan.FromSeconds(configuration.GetValue<int>("Dalan:IpLimitWindowSeconds"));
                var seconds = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds));

                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status429TooManyRequests;
                response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
                await response.WriteAsJsonAsync(
                    new { error = "rate_limited", message = $"Too many calls from this address. Wait {seconds} second(s) and try again." },
                    cancellationToken);
            };
        });
    }
}
