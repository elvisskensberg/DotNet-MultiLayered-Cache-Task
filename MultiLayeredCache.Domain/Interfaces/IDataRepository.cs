using MultiLayeredCache.Domain.Models;

namespace MultiLayeredCache.Domain.Interfaces;

/// <summary>
/// Interface for database repository operations
/// </summary>
public interface IDataRepository
{
    /// <summary>
    /// Retrieves data from the database by ID
    /// </summary>
    /// <param name="id">The unique identifier of the data</param>
    /// <returns>The data if found, null otherwise</returns>
    Task<CachedData?> GetByIdAsync(string id);

    /// <summary>
    /// Saves new data to the database
    /// </summary>
    /// <param name="data">The data to save</param>
    /// <returns>The ID of the saved data</returns>
    Task<string> SaveAsync(CachedData data);
}
