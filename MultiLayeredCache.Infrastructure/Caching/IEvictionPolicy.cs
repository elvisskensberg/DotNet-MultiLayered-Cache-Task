using MultiLayeredCache.Domain.Models;

namespace MultiLayeredCache.Infrastructure.Caching;

/// <summary>
/// Interface for cache eviction policies
/// Supports O(1) operations for Get, Set, and eviction
/// </summary>
public interface IEvictionPolicy
{
    /// <summary>
    /// Gets the maximum capacity of the cache
    /// </summary>
    int Capacity { get; }

    /// <summary>
    /// Gets the current count of items in the cache
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Attempts to retrieve a value from the cache
    /// </summary>
    /// <param name="key">The cache key</param>
    /// <param name="value">The cached value if found</param>
    /// <returns>True if the key exists, false otherwise</returns>
    bool TryGet(string key, out CachedData? value);

    /// <summary>
    /// Adds or updates a value in the cache
    /// If capacity is reached, evicts according to the policy
    /// </summary>
    /// <param name="key">The cache key</param>
    /// <param name="value">The value to cache</param>
    void Set(string key, CachedData value);

    /// <summary>
    /// Removes a specific item from the cache (cache invalidation)
    /// </summary>
    /// <param name="key">The cache key to remove</param>
    /// <returns>True if the item was removed, false if it didn't exist</returns>
    bool Remove(string key);

    /// <summary>
    /// Clears all items from the cache
    /// </summary>
    void Clear();
}
