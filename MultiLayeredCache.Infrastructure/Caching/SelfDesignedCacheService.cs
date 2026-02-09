using MultiLayeredCache.Domain.Interfaces;
using MultiLayeredCache.Domain.Models;

namespace MultiLayeredCache.Infrastructure.Caching;

/// <summary>
/// Self-Designed Caching System (SDCS) with configurable eviction policy.
/// Supports both LRU (Least Recently Used) and LFU (Least Frequently Used) strategies.
/// Implements in-memory caching with capacity constraints and O(1) operations.
/// </summary>
public class SelfDesignedCacheService : ICacheService
{
    private readonly IEvictionPolicy _evictionPolicy;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _strategyName;

    public string LayerName => $"SDCS ({_strategyName})";

    /// <summary>
    /// Initializes the SDCS with specified eviction policy
    /// </summary>
    /// <param name="evictionPolicy">The eviction policy to use (LRU or LFU)</param>
    /// <param name="strategyName">Name of the strategy for logging purposes</param>
    public SelfDesignedCacheService(IEvictionPolicy evictionPolicy, string strategyName)
    {
        _evictionPolicy = evictionPolicy ?? throw new ArgumentNullException(nameof(evictionPolicy));
        _strategyName = strategyName ?? "Unknown";
    }

    /// <summary>
    /// Retrieves data from the SDCS cache
    /// Time Complexity: O(1)
    /// </summary>
    public async Task<CachedData?> GetAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            if (_evictionPolicy.TryGet(id, out var value))
            {
                return value;
            }
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Stores data in the SDCS cache, evicting according to the configured policy if capacity is reached
    /// Time Complexity: O(1)
    /// </summary>
    public async Task SetAsync(CachedData data)
    {
        await _lock.WaitAsync();
        try
        {
            _evictionPolicy.Set(data.Id, data);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Removes data from the SDCS cache (cache invalidation)
    /// Used when data is updated to prevent stale cache entries
    /// Time Complexity: O(1)
    /// </summary>
    public async Task RemoveAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            _evictionPolicy.Remove(id);
        }
        finally
        {
            _lock.Release();
        }
    }
}
