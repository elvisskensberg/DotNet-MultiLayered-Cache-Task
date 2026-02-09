namespace MultiLayeredCache.Application.Features.GetData;

/// <summary>
/// Response DTO for data operations
/// </summary>
public class GetDataResponse
{
    /// <summary>
    /// Unique identifier of the data
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The actual value/content
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the data was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when the data was last accessed
    /// </summary>
    public DateTime LastAccessedAt { get; set; }
}
