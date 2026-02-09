using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MultiLayeredCache.Domain.Interfaces;
using MultiLayeredCache.Infrastructure.Caching;
using MultiLayeredCache.Infrastructure.Configuration;
using MultiLayeredCache.Infrastructure.HealthChecks;
using StackExchange.Redis;

namespace MultiLayeredCache.Api.Tests;

/// <summary>
/// Unit tests for health check implementations
/// Verifies health checks validate configuration and connectivity correctly
/// </summary>
public class HealthCheckTests
{
    #region SDCS Health Check Tests

    [Fact]
    public async Task SdcsHealthCheck_WithValidConfig_ReturnsHealthy()
    {
        // Arrange
        var options = Options.Create(new SdcsOptions
        {
            Capacity = 10
        });
        var healthCheck = new SdcsHealthCheck(options);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("configured correctly");
        result.Data.Should().ContainKey("capacity");
        result.Data["capacity"].Should().Be(10);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(101)]
    [InlineData(0)]
    public async Task SdcsHealthCheck_WithInvalidCapacity_ReturnsUnhealthy(int invalidCapacity)
    {
        // Arrange
        var options = Options.Create(new SdcsOptions
        {
            Capacity = invalidCapacity,
        });
        var healthCheck = new SdcsHealthCheck(options);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("invalid");
        result.Description.Should().Contain(invalidCapacity.ToString());
        result.Data["capacity"].Should().Be(invalidCapacity);
    }

    [Fact]
    public async Task SdcsHealthCheck_WithMinimumCapacity_ReturnsHealthy()
    {
        // Arrange
        var options = Options.Create(new SdcsOptions
        {
            Capacity = 3,
        });
        var healthCheck = new SdcsHealthCheck(options);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task SdcsHealthCheck_WithMaximumCapacity_ReturnsHealthy()
    {
        // Arrange
        var options = Options.Create(new SdcsOptions
        {
            Capacity = 100,
        });
        var healthCheck = new SdcsHealthCheck(options);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    #endregion

    #region CosmosDB Health Check Tests

    [Fact]
    public async Task CosmosDbHealthCheck_WithAccessibleCosmosDb_ReturnsHealthy()
    {
        // Arrange
        var mockCosmosClient = new Mock<CosmosClient>();
        mockCosmosClient
            .Setup(x => x.ReadAccountAsync())
            .ReturnsAsync((AccountProperties)null!); // Successful call returns account properties

        var mockLogger = new Mock<ILogger<CosmosDbHealthCheck>>();
        var healthCheck = new CosmosDbHealthCheck(mockCosmosClient.Object, mockLogger.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("accessible");
        mockCosmosClient.Verify(x => x.ReadAccountAsync(), Times.Once);
    }

    [Fact]
    public async Task CosmosDbHealthCheck_WhenCosmosDbUnavailable_ReturnsUnhealthy()
    {
        // Arrange
        var mockCosmosClient = new Mock<CosmosClient>();
        mockCosmosClient
            .Setup(x => x.ReadAccountAsync())
            .ThrowsAsync(new CosmosException("Connection failed", System.Net.HttpStatusCode.ServiceUnavailable, 0, "", 0));

        var mockLogger = new Mock<ILogger<CosmosDbHealthCheck>>();
        var healthCheck = new CosmosDbHealthCheck(mockCosmosClient.Object, mockLogger.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("not accessible");
        result.Exception.Should().NotBeNull();
    }

    #endregion

    #region Redis Health Check Tests

    [Fact]
    public async Task RedisHealthCheck_WithFastPing_ReturnsHealthy()
    {
        // Arrange
        var mockDatabase = new Mock<IDatabase>();
        mockDatabase.Setup(x => x.PingAsync(It.IsAny<CommandFlags>()))
            .ReturnsAsync(TimeSpan.FromMilliseconds(50));

        var mockRedis = new Mock<IConnectionMultiplexer>();
        mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDatabase.Object);
        mockRedis.Setup(x => x.GetEndPoints(It.IsAny<bool>()))
            .Returns(new System.Net.EndPoint[0]);

        var healthCheck = new RedisHealthCheck(mockRedis.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("responsive");
        result.Data.Should().ContainKey("ping_ms");
        result.Data["ping_ms"].Should().Be(50.0);
    }

    [Fact]
    public async Task RedisHealthCheck_WithSlowPing_ReturnsDegraded()
    {
        // Arrange
        var mockDatabase = new Mock<IDatabase>();
        mockDatabase.Setup(x => x.PingAsync(It.IsAny<CommandFlags>()))
            .ReturnsAsync(TimeSpan.FromMilliseconds(1500));

        var mockRedis = new Mock<IConnectionMultiplexer>();
        mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDatabase.Object);
        mockRedis.Setup(x => x.GetEndPoints(It.IsAny<bool>()))
            .Returns(new System.Net.EndPoint[0]);

        var healthCheck = new RedisHealthCheck(mockRedis.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("slow");
        result.Data["ping_ms"].Should().Be(1500.0);
    }

    [Fact]
    public async Task RedisHealthCheck_WithException_ReturnsUnhealthy()
    {
        // Arrange
        var mockDatabase = new Mock<IDatabase>();
        mockDatabase.Setup(x => x.PingAsync(It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

        var mockRedis = new Mock<IConnectionMultiplexer>();
        mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDatabase.Object);
        mockRedis.Setup(x => x.GetEndPoints(It.IsAny<bool>()))
            .Returns(new System.Net.EndPoint[0]);

        var healthCheck = new RedisHealthCheck(mockRedis.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("unavailable");
        result.Exception.Should().NotBeNull();
    }

    #endregion
}
