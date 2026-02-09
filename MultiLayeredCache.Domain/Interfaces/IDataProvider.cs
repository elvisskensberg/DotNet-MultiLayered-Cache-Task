using MultiLayeredCache.Domain.Models;

namespace MultiLayeredCache.Domain.Interfaces;

/// <summary>
/// Interface for data provider (supports both read and write operations)
/// Used for Decorator pattern implementation
/// Throws exceptions for error cases (handled by middleware)
/// </summary>
public interface IDataProvider
{
    /// <summary>
    /// Retrieves data by ID
    /// </summary>
    /// <param name="id">The unique identifier</param>
    /// <returns>The cached data</returns>
    /// <exception cref="NotFoundException">If data is not found</exception>
    Task<CachedData> GetByIdAsync(string id);

    /// <summary>
    /// Stores data
    /// </summary>
    /// <param name="data">The data to store</param>
    /// <returns>The ID of the stored data</returns>
    Task<string> SetAsync(CachedData data);
}
