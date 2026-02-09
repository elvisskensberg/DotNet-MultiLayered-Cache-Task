using System.ComponentModel.DataAnnotations;

namespace MultiLayeredCache.Infrastructure.Configuration;

/// <summary>
/// Configuration options for Self-Designed Caching System (SDCS)
/// Uses LRU (Least Recently Used) eviction strategy as per requirements
/// Options Pattern with data annotations for validation
/// </summary>
public class SdcsOptions
{
    public const string SectionName = "SDCS";

    /// <summary>
    /// Cache capacity (number of items)
    /// Must be between 3 and 100 (inclusive) as per task requirements
    /// </summary>
    [Range(3, 100, ErrorMessage = "SDCS Capacity must be between 3 and 100")]
    public int Capacity { get; set; } = 10;
}
