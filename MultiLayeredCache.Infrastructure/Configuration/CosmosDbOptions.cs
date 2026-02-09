using System.ComponentModel.DataAnnotations;

namespace MultiLayeredCache.Infrastructure.Configuration;

/// <summary>
/// Configuration options for Azure Cosmos DB
/// Validated using Data Annotations via Options Pattern validation
/// </summary>
public class CosmosDbOptions
{
    public const string SectionName = "CosmosDb";

    /// <summary>
    /// Cosmos DB connection string or account endpoint
    /// </summary>
    [Required(ErrorMessage = "CosmosDb:ConnectionString is required")]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Database name
    /// </summary>
    [Required(ErrorMessage = "CosmosDb:DatabaseName is required")]
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>
    /// Container name for storing cached data
    /// </summary>
    [Required(ErrorMessage = "CosmosDb:ContainerName is required")]
    public string ContainerName { get; set; } = string.Empty;

    /// <summary>
    /// Partition key path (default: /id)
    /// </summary>
    public string PartitionKeyPath { get; set; } = "/id";

    /// <summary>
    /// Whether to auto-create database and container if they don't exist
    /// </summary>
    public bool AutoCreateDatabase { get; set; } = true;
}
