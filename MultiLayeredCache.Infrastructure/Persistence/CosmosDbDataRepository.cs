using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiLayeredCache.Domain.Interfaces;
using MultiLayeredCache.Domain.Models;
using MultiLayeredCache.Infrastructure.Configuration;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace MultiLayeredCache.Infrastructure.Persistence;

/// <summary>
/// Azure Cosmos DB implementation of the data repository
/// Uses Cosmos DB NoSQL API for document storage with automatic partitioning
/// Wrapped with Polly resilience policies for production-grade reliability
/// </summary>
public class CosmosDbDataRepository : IDataRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosDbDataRepository> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;

    public CosmosDbDataRepository(
        CosmosClient cosmosClient,
        IOptions<CosmosDbOptions> options,
        ILogger<CosmosDbDataRepository> logger)
    {
        _logger = logger;
        var cosmosOptions = options.Value;

        // Get container reference (assumes DB/Container already exist in production)
        // For local dev with AutoCreateDatabase=true, use CosmosDbInitializationService
        _container = cosmosClient.GetContainer(cosmosOptions.DatabaseName, cosmosOptions.ContainerName);

        // Build Polly resilience pipeline for production-grade reliability
        _resiliencePipeline = BuildResiliencePipeline();

        _logger.LogInformation(
            "CosmosDB repository initialized with resilience policies: Database={DatabaseName}, Container={ContainerName}",
            cosmosOptions.DatabaseName,
            cosmosOptions.ContainerName);
    }

    /// <summary>
    /// Builds Polly resilience pipeline with retry, circuit breaker, and timeout policies
    /// Handles transient failures (429 throttling, 503 unavailable) gracefully
    /// </summary>
    private ResiliencePipeline BuildResiliencePipeline()
    {
        return new ResiliencePipelineBuilder()
            // 1. Retry policy for transient Cosmos DB failures
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<CosmosException>(ex =>
                    ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||        // 429 throttling
                    ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||     // 503 unavailable
                    ex.StatusCode == System.Net.HttpStatusCode.RequestTimeout),          // 408 timeout
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Cosmos DB operation failed (Attempt {AttemptNumber}/{MaxRetryAttempts}). Retrying in {RetryDelay}ms. Exception: {Exception}",
                        args.AttemptNumber,
                        3,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message ?? "Unknown");
                    return ValueTask.CompletedTask;
                }
            })
            // 2. Circuit breaker to prevent cascading failures
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<CosmosException>(),
                FailureRatio = 0.5,           // Open circuit if 50% of requests fail
                MinimumThroughput = 10,        // Require at least 10 requests before breaking
                BreakDuration = TimeSpan.FromSeconds(30),
                OnOpened = args =>
                {
                    _logger.LogError(
                        "Cosmos DB circuit breaker opened due to {FailureRate:P} failure rate. Circuit will remain open for {BreakDuration}s",
                        args.BreakDuration.TotalSeconds,
                        args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation("Cosmos DB circuit breaker closed. Normal operations resumed");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    _logger.LogInformation("Cosmos DB circuit breaker half-opened. Testing if service recovered");
                    return ValueTask.CompletedTask;
                }
            })
            // 3. Timeout policy to prevent hanging requests
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(10),
                OnTimeout = args =>
                {
                    _logger.LogWarning(
                        "Cosmos DB operation timed out after {Timeout}s",
                        args.Timeout.TotalSeconds);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Retrieves data from Cosmos DB by ID
    /// Wrapped with Polly resilience pipeline for retry and circuit breaker
    /// </summary>
    public async Task<CachedData?> GetByIdAsync(string id)
    {
        try
        {
            return await _resiliencePipeline.ExecuteAsync(async cancellationToken =>
            {
                var response = await _container.ReadItemAsync<CachedData>(
                    id,
                    new PartitionKey(id),
                    cancellationToken: cancellationToken);

                _logger.LogDebug("Retrieved item {Id} from Cosmos DB", id);
                return response.Resource;
            });
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogDebug("Item {Id} not found in Cosmos DB", id);
            return null;
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "Cosmos DB circuit breaker is open. Item {Id} retrieval failed", id);
            throw;
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogError(ex, "Cosmos DB operation timed out for item {Id}", id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving item {Id} from Cosmos DB after retry attempts", id);
            throw;
        }
    }

    /// <summary>
    /// Saves data to Cosmos DB
    /// Wrapped with Polly resilience pipeline for retry and circuit breaker
    /// </summary>
    public async Task<string> SaveAsync(CachedData data)
    {
        try
        {
            // Generate ID if not provided
            if (string.IsNullOrEmpty(data.Id))
            {
                data.Id = Guid.NewGuid().ToString();
            }

            // Set timestamps
            data.CreatedAt = DateTime.UtcNow;
            data.LastAccessedAt = DateTime.UtcNow;

            return await _resiliencePipeline.ExecuteAsync(async cancellationToken =>
            {
                // Upsert to Cosmos DB (create or replace)
                var response = await _container.UpsertItemAsync(
                    data,
                    new PartitionKey(data.Id),
                    cancellationToken: cancellationToken);

                _logger.LogDebug(
                    "Saved item {Id} to Cosmos DB (Request charge: {RequestCharge} RU)",
                    data.Id,
                    response.RequestCharge);

                return data.Id;
            });
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "Cosmos DB circuit breaker is open. Save operation failed");
            throw;
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogError(ex, "Cosmos DB save operation timed out");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving item to Cosmos DB after retry attempts");
            throw;
        }
    }
}
