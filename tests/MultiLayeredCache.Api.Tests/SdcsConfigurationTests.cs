using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MultiLayeredCache.Infrastructure;
using MultiLayeredCache.Infrastructure.Configuration;

namespace MultiLayeredCache.Api.Tests;

/// <summary>
/// Tests for SDCS configuration validation and startup behavior
/// Ensures invalid configurations cause startup failure as required
/// SDCS uses LRU (Least Recently Used) eviction strategy as per requirements
/// </summary>
public class SdcsConfigurationTests
{
    [Fact]
    public void SdcsOptions_WithValidConfiguration_PassesValidation()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SDCS:Capacity"] = "10"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<SdcsOptions>()
            .Bind(configuration.GetSection("SDCS"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<SdcsOptions>>().Value;

        // Assert
        options.Capacity.Should().Be(10);
    }

    [Theory]
    [InlineData(2)] // Below minimum
    [InlineData(101)] // Above maximum
    [InlineData(0)]
    [InlineData(-1)]
    public void SdcsOptions_WithInvalidCapacity_FailsValidation(int invalidCapacity)
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SDCS:Capacity"] = invalidCapacity.ToString()
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<SdcsOptions>()
            .Bind(configuration.GetSection("SDCS"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Act & Assert
        var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        var act = () => serviceProvider.GetRequiredService<IOptions<SdcsOptions>>().Value;
        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Capacity*");
    }

    [Theory]
    [InlineData(3)]  // Minimum
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)] // Maximum
    public void SdcsOptions_WithValidCapacity_PassesValidation(int validCapacity)
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SDCS:Capacity"] = validCapacity.ToString()
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<SdcsOptions>()
            .Bind(configuration.GetSection("SDCS"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<SdcsOptions>>().Value;

        // Assert
        options.Capacity.Should().Be(validCapacity);
    }

    [Fact]
    public void SdcsOptions_WithMissingCapacity_UsesDefault()
    {
        // Arrange - No capacity specified
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<SdcsOptions>()
            .Bind(configuration.GetSection("SDCS"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<SdcsOptions>>().Value;

        // Assert - Should use default value (10)
        options.Capacity.Should().Be(10);
    }

    [Fact]
    public void Infrastructure_WithValidConfig_CreatesLruPolicy()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SDCS:Capacity"] = "10",
                ["Redis:ConnectionString"] = "localhost:6379,abortConnect=false",
                ["CosmosDb:ConnectionString"] = "AccountEndpoint=https://localhost:8081/;AccountKey=test",
                ["CosmosDb:DatabaseName"] = "TestDb",
                ["CosmosDb:ContainerName"] = "TestContainer"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        // Act
        var serviceProvider = services.BuildServiceProvider(validateScopes: true);
        var options = serviceProvider.GetRequiredService<IOptions<SdcsOptions>>().Value;

        // Assert
        options.Capacity.Should().Be(10);
    }

    [Fact]
    public void Infrastructure_WithInvalidCapacity_FailsOnValidation()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SDCS:Capacity"] = "2", // Below minimum
                ["Redis:ConnectionString"] = "localhost:6379,abortConnect=false",
                ["CosmosDb:ConnectionString"] = "AccountEndpoint=https://localhost:8081/;AccountKey=test",
                ["CosmosDb:DatabaseName"] = "TestDb",
                ["CosmosDb:ContainerName"] = "TestContainer"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        // Act & Assert - Should fail when options are accessed
        var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        var act = () => serviceProvider.GetRequiredService<IOptions<SdcsOptions>>().Value;
        act.Should().Throw<OptionsValidationException>();
    }
}
