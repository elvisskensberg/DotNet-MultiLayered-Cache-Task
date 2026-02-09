using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace MultiLayeredCache.Infrastructure.HealthChecks;

/// <summary>
/// Health check for Redis cache connectivity
/// </summary>
public class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;

    public RedisHealthCheck(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var database = _redis.GetDatabase();

            // Perform a simple ping operation
            var ping = await database.PingAsync();

            if (ping.TotalMilliseconds > 1000)
            {
                return HealthCheckResult.Degraded(
                    $"Redis is slow (ping: {ping.TotalMilliseconds}ms)",
                    data: new Dictionary<string, object>
                    {
                        ["ping_ms"] = ping.TotalMilliseconds,
                        ["endpoints"] = string.Join(", ", _redis.GetEndPoints())
                    });
            }

            return HealthCheckResult.Healthy(
                "Redis is responsive",
                data: new Dictionary<string, object>
                {
                    ["ping_ms"] = ping.TotalMilliseconds,
                    ["endpoints"] = string.Join(", ", _redis.GetEndPoints()),
                    ["connected_endpoints"] = _redis.GetEndPoints().Length
                });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Redis is unavailable",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    ["endpoints"] = string.Join(", ", _redis.GetEndPoints())
                });
        }
    }
}
