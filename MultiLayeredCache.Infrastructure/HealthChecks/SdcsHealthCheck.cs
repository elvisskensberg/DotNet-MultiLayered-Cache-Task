using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MultiLayeredCache.Infrastructure.Configuration;

namespace MultiLayeredCache.Infrastructure.HealthChecks;

/// <summary>
/// Health check for Self-Designed Cache Service (SDCS)
/// Uses LRU (Least Recently Used) eviction strategy as per requirements
/// </summary>
public class SdcsHealthCheck : IHealthCheck
{
    private readonly SdcsOptions _options;

    public SdcsHealthCheck(IOptions<SdcsOptions> options)
    {
        _options = options.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Verify capacity is valid
            if (_options.Capacity < 3 || _options.Capacity > 100)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"SDCS capacity is invalid: {_options.Capacity} (must be 3-100)",
                    data: new Dictionary<string, object>
                    {
                        ["capacity"] = _options.Capacity,
                        ["min_capacity"] = 3,
                        ["max_capacity"] = 100,
                        ["eviction_strategy"] = "LRU"
                    }));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                "SDCS is configured correctly with LRU eviction",
                data: new Dictionary<string, object>
                {
                    ["capacity"] = _options.Capacity,
                    ["eviction_strategy"] = "LRU",
                    ["status"] = "operational"
                }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "SDCS configuration error",
                exception: ex));
        }
    }
}
