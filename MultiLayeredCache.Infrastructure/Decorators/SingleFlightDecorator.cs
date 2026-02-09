using Microsoft.Extensions.Logging;
using MultiLayeredCache.Domain.Interfaces;
using MultiLayeredCache.Domain.Models;
using System.Collections.Concurrent;

namespace MultiLayeredCache.Infrastructure.Decorators;

/// <summary>
/// Decorator that prevents cache stampede by ensuring only one request per key reaches the inner provider
/// Also known as "request coalescing" or "single-flight" pattern
/// Prevents thundering herd when many concurrent requests for the same key miss cache
/// </summary>
public class SingleFlightDecorator : IDataProvider
{
    private readonly IDataProvider _innerProvider;
    private readonly ILogger<SingleFlightDecorator> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public SingleFlightDecorator(
        IDataProvider innerProvider,
        ILogger<SingleFlightDecorator> logger)
    {
        _innerProvider = innerProvider;
        _logger = logger;
    }

    /// <summary>
    /// Gets data with stampede protection - only ONE concurrent request per key reaches inner provider
    /// Other concurrent requests for the same key wait and benefit from the first request's cache population
    /// </summary>
    public async Task<CachedData> GetByIdAsync(string id)
    {
        // Get or create a semaphore for this specific key
        var lockObj = _locks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));

        // Track if this is the "winning" request or a "waiting" request
        var isFirstRequest = lockObj.CurrentCount == 1;

        await lockObj.WaitAsync();
        try
        {
            if (!isFirstRequest)
            {
                _logger.LogDebug("Request for {Id} waited for concurrent request (stampede protection)", id);
            }

            // Delegate to inner provider (which includes cache layers)
            // If this is a waiting request, the cache may now be populated by the first request
            return await _innerProvider.GetByIdAsync(id);
        }
        finally
        {
            lockObj.Release();

            // Clean up the semaphore if no one else is waiting
            // This prevents unbounded memory growth for one-off keys
            if (lockObj.CurrentCount == 1 && _locks.TryRemove(id, out _))
            {
                _logger.LogTrace("Removed semaphore for {Id} (no pending requests)", id);
            }
        }
    }

    /// <summary>
    /// Writes through to inner provider (no stampede protection needed for writes)
    /// </summary>
    public async Task<string> SetAsync(CachedData data)
    {
        return await _innerProvider.SetAsync(data);
    }
}
