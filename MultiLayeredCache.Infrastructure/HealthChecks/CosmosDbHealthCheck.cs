using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace MultiLayeredCache.Infrastructure.HealthChecks;

/// <summary>
/// Health check for Azure Cosmos DB connectivity and latency
/// Validates regional colocation by measuring response time
/// </summary>
public class CosmosDbHealthCheck : IHealthCheck
{
    private readonly CosmosClient _cosmosClient;
    private readonly ILogger<CosmosDbHealthCheck> _logger;
    private const int WarningLatencyMs = 10; // Warn if latency > 10ms (suggests cross-region)
    private const int DegradedLatencyMs = 50; // Degraded if latency > 50ms

    public CosmosDbHealthCheck(
        CosmosClient cosmosClient,
        ILogger<CosmosDbHealthCheck> logger)
    {
        _cosmosClient = cosmosClient;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Measure latency to Cosmos DB
            var stopwatch = Stopwatch.StartNew();
            await _cosmosClient.ReadAccountAsync();
            stopwatch.Stop();

            var latencyMs = stopwatch.ElapsedMilliseconds;

            var data = new Dictionary<string, object>
            {
                ["latency_ms"] = latencyMs,
                ["connection_mode"] = "Direct"
            };

            // Determine health status based on latency
            if (latencyMs > DegradedLatencyMs)
            {
                _logger.LogWarning(
                    "Cosmos DB latency is high: {LatencyMs}ms (expected <10ms for same-region). Consider regional colocation.",
                    latencyMs);

                return HealthCheckResult.Degraded(
                    $"Cosmos DB accessible but high latency ({latencyMs}ms)",
                    data: data);
            }

            if (latencyMs > WarningLatencyMs)
            {
                _logger.LogDebug(
                    "Cosmos DB latency: {LatencyMs}ms (ideal <10ms for same-region)",
                    latencyMs);

                // Still healthy, but log a note about latency
                return HealthCheckResult.Healthy(
                    $"Cosmos DB accessible ({latencyMs}ms latency - consider regional colocation for optimal performance)",
                    data: data);
            }

            _logger.LogDebug("Cosmos DB health check passed: {LatencyMs}ms", latencyMs);
            return HealthCheckResult.Healthy(
                $"Cosmos DB accessible with low latency ({latencyMs}ms)",
                data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cosmos DB health check failed");
            return HealthCheckResult.Unhealthy(
                "Cosmos DB is not accessible",
                ex);
        }
    }
}
