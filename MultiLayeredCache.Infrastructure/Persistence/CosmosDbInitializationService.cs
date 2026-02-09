using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiLayeredCache.Infrastructure.Configuration;

namespace MultiLayeredCache.Infrastructure.Persistence;

/// <summary>
/// Hosted service that initializes Cosmos DB database and container on startup
/// Avoids sync-over-async anti-pattern in repository constructor
/// Runs once during application startup before accepting requests
/// </summary>
public class CosmosDbInitializationService : IHostedService
{
    private readonly CosmosClient _cosmosClient;
    private readonly CosmosDbOptions _options;
    private readonly ILogger<CosmosDbInitializationService> _logger;

    public CosmosDbInitializationService(
        CosmosClient cosmosClient,
        IOptions<CosmosDbOptions> options,
        ILogger<CosmosDbInitializationService> logger)
    {
        _cosmosClient = cosmosClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.AutoCreateDatabase)
        {
            _logger.LogInformation(
                "Skipping Cosmos DB initialization (AutoCreateDatabase=false). Assuming database and container exist.");
            return;
        }

        try
        {
            _logger.LogInformation(
                "Initializing Cosmos DB: Database={DatabaseName}, Container={ContainerName}",
                _options.DatabaseName,
                _options.ContainerName);

            // Create database if not exists
            var databaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(
                _options.DatabaseName,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Database '{DatabaseName}' initialized (Status: {StatusCode})",
                _options.DatabaseName,
                (int)databaseResponse.StatusCode);

            // Create container if not exists
            var containerResponse = await databaseResponse.Database.CreateContainerIfNotExistsAsync(
                _options.ContainerName,
                _options.PartitionKeyPath,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Container '{ContainerName}' initialized (Status: {StatusCode}, RU: {RequestCharge})",
                _options.ContainerName,
                (int)containerResponse.StatusCode,
                containerResponse.RequestCharge);

            _logger.LogInformation("Cosmos DB initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Cosmos DB initialization failed. Ensure connection string is valid and database is accessible.");
            throw; // Fail fast if DB initialization fails
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // No cleanup needed
        return Task.CompletedTask;
    }
}
