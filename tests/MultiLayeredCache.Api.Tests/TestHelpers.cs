using MultiLayeredCache.Domain.Models;

namespace MultiLayeredCache.Api.Tests;

/// <summary>
/// Shared test helper methods for creating test data
/// Replaces complex TestInfrastructure with simple, clear helpers
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Creates test data with optional custom ID and value
    /// </summary>
    public static CachedData CreateTestData(string? id = null, string? value = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Value = value ?? $"test-value-{Guid.NewGuid()}",
        CreatedAt = DateTime.UtcNow,
        LastAccessedAt = DateTime.UtcNow
    };
}
