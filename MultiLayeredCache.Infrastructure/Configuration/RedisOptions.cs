using System.ComponentModel.DataAnnotations;

namespace MultiLayeredCache.Infrastructure.Configuration;

/// <summary>
/// Configuration options for Redis Cache
/// Uses Options Pattern with data annotations for validation
/// </summary>
public class RedisOptions
{
    public const string SectionName = "Redis";

    /// <summary>
    /// Redis connection string
    /// </summary>
    [Required(ErrorMessage = "Redis ConnectionString is required")]
    public string ConnectionString { get; set; } = "localhost:6379,abortConnect=false";

    /// <summary>
    /// TTL for cached items in minutes
    /// </summary>
    [Range(1, 1440, ErrorMessage = "TTL must be between 1 and 1440 minutes (24 hours)")]
    public int TtlMinutes { get; set; } = 5;
}
