using MultiLayeredCache.Domain.Interfaces;
using MultiLayeredCache.Domain.Models;
using System.Text.Json;
using StackExchange.Redis;

namespace MultiLayeredCache.Infrastructure.Caching;

/// <summary>
/// Redis-based distributed cache service with TTL of 5 minutes
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private static readonly TimeSpan CacheTTL = TimeSpan.FromMinutes(5);

    public string LayerName => "Redis Cache";

    public RedisCacheService(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _database = redis.GetDatabase();
    }

    /// <summary>
    /// Retrieves data from Redis cache
    /// </summary>
    public async Task<CachedData?> GetAsync(string id)
    {
        var value = await _database.StringGetAsync(id);
        if (value.IsNullOrEmpty)
        {
            return null;
        }

        return JsonSerializer.Deserialize<CachedData>((string)value!);
    }

    /// <summary>
    /// Stores data in Redis cache with 5-minute TTL
    /// </summary>
    public async Task SetAsync(CachedData data)
    {
        var serialized = JsonSerializer.Serialize(data);
        await _database.StringSetAsync(data.Id, serialized, CacheTTL);
    }

    /// <summary>
    /// Removes data from Redis cache (cache invalidation)
    /// Used when data is updated to prevent stale cache entries
    /// </summary>
    public async Task RemoveAsync(string id)
    {
        await _database.KeyDeleteAsync(id);
    }
}
