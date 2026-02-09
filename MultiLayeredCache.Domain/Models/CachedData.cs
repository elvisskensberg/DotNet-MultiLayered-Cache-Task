namespace MultiLayeredCache.Domain.Models;

/// <summary>
/// Represents the data entity stored in the multi-layered cache system
/// </summary>
public class CachedData
{
    /// <summary>
    /// Unique identifier for the data
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The actual content/value being cached
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the data was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the data was last accessed (for LRU tracking)
    /// </summary>
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
}
