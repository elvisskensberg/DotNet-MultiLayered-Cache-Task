using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MultiLayeredCache.Infrastructure;
using MultiLayeredCache.Infrastructure.Configuration;

namespace MultiLayeredCache.Api.Tests;

/// <summary>
/// Tests for Infrastructure DependencyInjection configuration
/// Verifies Options Pattern validation with ValidateOnStart and ValidateDataAnnotations
/// </summary>
public class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_WithValidConfiguration_ShouldSucceed()
    {
        // Arrange
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["SDCS:Capacity"] = "10",
            ["Redis:ConnectionString"] = "localhost:6379",
            ["CosmosDb:ConnectionString"] = "AccountEndpoint=https://localhost:8081/;AccountKey=test",
            ["CosmosDb:DatabaseName"] = "TestDb",
            ["CosmosDb:ContainerName"] = "TestContainer"
        });

        var services = new ServiceCollection();
        services.AddLogging(); // Required for CosmosDbDataRepository

        // Act
        services.AddInfrastructure(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Validate options are created successfully
        var sdcsOptions = serviceProvider.GetRequiredService<IOptions<SdcsOptions>>().Value;
        sdcsOptions.Capacity.Should().Be(10);

        var redisOptions = serviceProvider.GetRequiredService<IOptions<RedisOptions>>().Value;
        redisOptions.ConnectionString.Should().Be("localhost:6379");

        var cosmosOptions = serviceProvider.GetRequiredService<IOptions<CosmosDbOptions>>().Value;
        cosmosOptions.DatabaseName.Should().Be("TestDb");
    }

    [Fact]
    public void AddInfrastructure_WithMissingSdcsConfiguration_ShouldUseDefaultValue()
    {
        // Arrange - Missing SDCS section entirely
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Redis:ConnectionString"] = "localhost:6379",
            ["CosmosDb:ConnectionString"] = "AccountEndpoint=https://localhost:8081/;AccountKey=test",
            ["CosmosDb:DatabaseName"] = "TestDb",
            ["CosmosDb:ContainerName"] = "TestContainer"
        });

        var services = new ServiceCollection();
        services.AddLogging(); // Required for CosmosDbDataRepository
        services.AddInfrastructure(configuration);

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var sdcsOptions = serviceProvider.GetRequiredService<IOptions<SdcsOptions>>().Value;

        // Assert - Should use default value from SdcsOptions class
        sdcsOptions.Capacity.Should().Be(10); // Default value
    }

    [Fact]
    public void AddInfrastructure_WithMissingRedisConfiguration_ShouldUseDefaultValue()
    {
        // Arrange - Missing Redis section entirely
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["SDCS:Capacity"] = "10"
        });

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var redisOptions = serviceProvider.GetRequiredService<IOptions<RedisOptions>>().Value;

        // Assert - Should use default value from RedisOptions class
        redisOptions.ConnectionString.Should().Be("localhost:6379,abortConnect=false"); // Default value
    }

    [Fact]
    public void AddInfrastructure_WithSdcsCapacityTooLow_ShouldFailOnStartup()
    {
        // Arrange - Capacity below minimum (< 3)
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["SDCS:Capacity"] = "2",
            ["Redis:ConnectionString"] = "localhost:6379",
            ["CosmosDb:ConnectionString"] = "AccountEndpoint=https://localhost:8081/;AccountKey=test",
            ["CosmosDb:DatabaseName"] = "TestDb",
            ["CosmosDb:ContainerName"] = "TestContainer"
        });

        var services = new ServiceCollection();
        services.AddLogging(); // Required for CosmosDbDataRepository
        services.AddInfrastructure(configuration);

        // Act & Assert - Should throw due to DataAnnotations validation
        var act = () =>
        {
            var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
            // Trigger validation
            var _ = serviceProvider.GetRequiredService<IOptions<SdcsOptions>>().Value;
        };

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Capacity must be between 3 and 100*");
    }

    [Fact]
    public void AddInfrastructure_WithSdcsCapacityTooHigh_ShouldFailOnStartup()
    {
        // Arrange - Capacity above maximum (> 100)
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["SDCS:Capacity"] = "101",
            ["Redis:ConnectionString"] = "localhost:6379",
            ["CosmosDb:ConnectionString"] = "AccountEndpoint=https://localhost:8081/;AccountKey=test",
            ["CosmosDb:DatabaseName"] = "TestDb",
            ["CosmosDb:ContainerName"] = "TestContainer"
        });

        var services = new ServiceCollection();
        services.AddLogging(); // Required for CosmosDbDataRepository
        services.AddInfrastructure(configuration);

        // Act & Assert - Should throw due to DataAnnotations validation
        var act = () =>
        {
            var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
            // Trigger validation
            var _ = serviceProvider.GetRequiredService<IOptions<SdcsOptions>>().Value;
        };

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Capacity must be between 3 and 100*");
    }

    [Fact]
    public void AddInfrastructure_WithSdcsCapacityAtMinimumBoundary_ShouldSucceed()
    {
        // Arrange - Capacity at minimum (3)
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["SDCS:Capacity"] = "3",
            ["Redis:ConnectionString"] = "localhost:6379"
        });

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var sdcsOptions = serviceProvider.GetRequiredService<IOptions<SdcsOptions>>().Value;

        // Assert
        sdcsOptions.Capacity.Should().Be(3);
    }

    [Fact]
    public void AddInfrastructure_WithSdcsCapacityAtMaximumBoundary_ShouldSucceed()
    {
        // Arrange - Capacity at maximum (100)
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["SDCS:Capacity"] = "100",
            ["Redis:ConnectionString"] = "localhost:6379"
        });

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var sdcsOptions = serviceProvider.GetRequiredService<IOptions<SdcsOptions>>().Value;

        // Assert
        sdcsOptions.Capacity.Should().Be(100);
    }

    [Fact]
    public void AddInfrastructure_WithEmptyRedisConnectionString_ShouldFailOnStartup()
    {
        // Arrange - Empty connection string
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["SDCS:Capacity"] = "10",
            ["Redis:ConnectionString"] = "",
            ["CosmosDb:ConnectionString"] = "AccountEndpoint=https://localhost:8081/;AccountKey=test",
            ["CosmosDb:DatabaseName"] = "TestDb",
            ["CosmosDb:ContainerName"] = "TestContainer"
        });

        var services = new ServiceCollection();
        services.AddLogging(); // Required for CosmosDbDataRepository
        services.AddInfrastructure(configuration);

        // Act & Assert - Should throw due to Required validation
        var act = () =>
        {
            var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
            // Trigger validation
            var _ = serviceProvider.GetRequiredService<IOptions<RedisOptions>>().Value;
        };

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*ConnectionString*required*");
    }

    /// <summary>
    /// Helper method to create IConfiguration from dictionary
    /// </summary>
    private static IConfiguration CreateConfiguration(Dictionary<string, string?> configData)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }
}
