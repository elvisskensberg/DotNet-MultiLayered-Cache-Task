using MultiLayeredCache.Domain.Models;

namespace MultiLayeredCache.Domain.Interfaces;

/// <summary>
/// Interface for cache service operations
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Retrieves data from the cache by ID
    /// </summary>
    /// <param name="id">The unique identifier of the data</param>
    /// <returns>The cached data if found, null otherwise</returns>
    Task<CachedData?> GetAsync(string id);

    /// <summary>
    /// Stores data in the cache
    /// </summary>
    /// <param name="data">The data to cache</param>
    Task SetAsync(CachedData data);

    /// <summary>
    /// Removes data from the cache by ID (cache invalidation)
    /// Used when data is updated to prevent stale cache entries
    /// </summary>
    /// <param name="id">The unique identifier of the data to remove</param>
    Task RemoveAsync(string id);

    /// <summary>
    /// Gets the name of the cache layer (for logging/debugging)
    /// </summary>
    string LayerName { get; }
}
