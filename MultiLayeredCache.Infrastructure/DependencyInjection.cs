using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiLayeredCache.Domain.Interfaces;
using MultiLayeredCache.Infrastructure.Caching;
using MultiLayeredCache.Infrastructure.Configuration;
using MultiLayeredCache.Infrastructure.Decorators;
using MultiLayeredCache.Infrastructure.Persistence;
using StackExchange.Redis;

namespace MultiLayeredCache.Infrastructure;

/// <summary>
/// Extension methods for configuring Infrastructure layer services
/// Uses Options Pattern and Decorator Pattern with IDataProvider
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds infrastructure services to the DI container with Decorator pattern and Options
    /// Builds the chain: Repository -> SDCS Cache -> Redis Cache (IDataProvider)
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure Options Pattern with validation
        services.AddOptions<SdcsOptions>()
            .Bind(configuration.GetSection(SdcsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<CosmosDbOptions>()
            .Bind(configuration.GetSection(CosmosDbOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register Cosmos DB Client with optimized settings for production
        services.AddSingleton<CosmosClient>(sp =>
        {
            var cosmosOptions = sp.GetRequiredService<IOptions<CosmosDbOptions>>().Value;
            return new CosmosClient(cosmosOptions.ConnectionString, new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Direct, // Better performance than Gateway
                MaxRetryAttemptsOnRateLimitedRequests = 3, // Polly handles retries
                MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(10),
                ConsistencyLevel = ConsistencyLevel.Session, // Read-your-writes guarantee
                RequestTimeout = TimeSpan.FromSeconds(10), // Match Polly timeout
                EnableContentResponseOnWrite = false, // Performance: don't return full response body on writes
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
            });
        });

        // Register Cosmos DB initialization hosted service (runs on startup, avoids sync-over-async)
        services.AddHostedService<CosmosDbInitializationService>();

        // Register Repository (Cosmos DB)
        services.AddSingleton<IDataRepository, CosmosDbDataRepository>();

        // Register Redis Connection
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var redisOptions = sp.GetRequiredService<IOptions<RedisOptions>>().Value;
            return ConnectionMultiplexer.Connect(redisOptions.ConnectionString);
        });

        // Build the Decorator chain: Repository -> SDCS -> Redis -> SingleFlight (IDataProvider)
        services.AddSingleton<IDataProvider>(sp =>
        {
            var repository = sp.GetRequiredService<IDataRepository>();
            var redis = sp.GetRequiredService<IConnectionMultiplexer>();
            var sdcsOptions = sp.GetRequiredService<IOptions<SdcsOptions>>().Value;
            var cacheLogger = sp.GetRequiredService<ILogger<CacheProviderDecorator>>();
            var singleFlightLogger = sp.GetRequiredService<ILogger<SingleFlightDecorator>>();

            // Layer 1: Repository (innermost)
            IDataProvider dataProvider = new RepositoryDataProvider(repository);

            // Layer 2: SDCS Cache (wraps repository) - Uses LRU eviction strategy as per requirements
            var evictionPolicy = new LruEvictionPolicy(sdcsOptions.Capacity);
            var sdcsCache = new SelfDesignedCacheService(evictionPolicy, "LRU");
            dataProvider = new CacheProviderDecorator(dataProvider, sdcsCache, "SDCS", cacheLogger);

            // Layer 3: Redis Cache (wraps SDCS)
            var redisCache = new RedisCacheService(redis);
            dataProvider = new CacheProviderDecorator(dataProvider, redisCache, "Redis", cacheLogger);

            // Layer 4: Single-Flight protection (outermost) - Prevents cache stampede
            dataProvider = new SingleFlightDecorator(dataProvider, singleFlightLogger);

            return dataProvider;
        });

        return services;
    }
}
