using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MultiLayeredCache.Domain.Models;
using MultiLayeredCache.Infrastructure.Configuration;
using MultiLayeredCache.Infrastructure.Persistence;
using Polly.CircuitBreaker;
using System.Net;

namespace MultiLayeredCache.Api.Tests;

/// <summary>
/// Unit tests for Cosmos DB resilience policies (Polly integration)
/// Tests retry behavior, circuit breaker, and timeout handling
/// </summary>
public class CosmosDbResilienceTests
{
    private readonly Mock<ILogger<CosmosDbDataRepository>> _mockLogger;
    private readonly CosmosDbOptions _cosmosOptions;

    public CosmosDbResilienceTests()
    {
        _mockLogger = new Mock<ILogger<CosmosDbDataRepository>>();
        _cosmosOptions = new CosmosDbOptions
        {
            ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=test",
            DatabaseName = "TestDb",
            ContainerName = "TestContainer",
            AutoCreateDatabase = false
        };
    }

    [Fact]
    public async Task GetByIdAsync_WithTransientFailure_RetriesAndSucceeds()
    {
        // Arrange
        var mockClient = new Mock<CosmosClient>();
        var mockContainer = new Mock<Container>();
        var attemptCount = 0;
        var testData = TestHelpers.CreateTestData("test-id", "test-value");

        mockClient.Setup(x => x.GetContainer(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(mockContainer.Object);

        // First attempt fails with 429, second succeeds
        mockContainer.Setup(x => x.ReadItemAsync<CachedData>(
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                attemptCount++;
                if (attemptCount == 1)
                {
                    throw new CosmosException("Too Many Requests", HttpStatusCode.TooManyRequests, 429, "activity-id", 1.0);
                }
                return CreateMockItemResponse(testData);
            });

        var repository = new CosmosDbDataRepository(
            mockClient.Object,
            Options.Create(_cosmosOptions),
            _mockLogger.Object);

        // Act
        var result = await repository.GetByIdAsync("test-id");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("test-id");
        attemptCount.Should().Be(2); // First attempt failed, second succeeded

        // Verify retry was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cosmos DB operation failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SaveAsync_WithServiceUnavailable_RetriesAndSucceeds()
    {
        // Arrange
        var mockClient = new Mock<CosmosClient>();
        var mockContainer = new Mock<Container>();
        var attemptCount = 0;
        var testData = TestHelpers.CreateTestData("new-id", "new-value");

        mockClient.Setup(x => x.GetContainer(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(mockContainer.Object);

        // First two attempts fail with 503, third succeeds
        mockContainer.Setup(x => x.UpsertItemAsync(
                It.IsAny<CachedData>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                attemptCount++;
                if (attemptCount <= 2)
                {
                    throw new CosmosException("Service Unavailable", HttpStatusCode.ServiceUnavailable, 503, "activity-id", 1.0);
                }
                return CreateMockItemResponse(testData);
            });

        var repository = new CosmosDbDataRepository(
            mockClient.Object,
            Options.Create(_cosmosOptions),
            _mockLogger.Object);

        // Act
        var result = await repository.SaveAsync(testData);

        // Assert
        result.Should().NotBeNullOrEmpty();
        attemptCount.Should().Be(3); // Two failures, then success

        // Verify retries were logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cosmos DB operation failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task GetByIdAsync_WithPersistentFailure_ExhaustsRetriesAndThrows()
    {
        // Arrange
        var mockClient = new Mock<CosmosClient>();
        var mockContainer = new Mock<Container>();
        var attemptCount = 0;

        mockClient.Setup(x => x.GetContainer(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(mockContainer.Object);

        // All attempts fail with 429
        mockContainer.Setup(x => x.ReadItemAsync<CachedData>(
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                attemptCount++;
                return Task.FromException<ItemResponse<CachedData>>(
                    new CosmosException("Too Many Requests", HttpStatusCode.TooManyRequests, 429, "activity-id", 1.0));
            });

        var repository = new CosmosDbDataRepository(
            mockClient.Object,
            Options.Create(_cosmosOptions),
            _mockLogger.Object);

        // Act & Assert
        var act = () => repository.GetByIdAsync("test-id");
        await act.Should().ThrowAsync<CosmosException>();

        // Should retry 3 times (initial + 3 retries = 4 total attempts)
        attemptCount.Should().Be(4);

        // Verify all retries were logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cosmos DB operation failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task GetByIdAsync_WithNotFound_DoesNotRetry()
    {
        // Arrange
        var mockClient = new Mock<CosmosClient>();
        var mockContainer = new Mock<Container>();
        var attemptCount = 0;

        mockClient.Setup(x => x.GetContainer(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(mockContainer.Object);

        // NotFound should not trigger retry
        mockContainer.Setup(x => x.ReadItemAsync<CachedData>(
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                attemptCount++;
                return Task.FromException<ItemResponse<CachedData>>(
                    new CosmosException("Not Found", HttpStatusCode.NotFound, 404, "activity-id", 1.0));
            });

        var repository = new CosmosDbDataRepository(
            mockClient.Object,
            Options.Create(_cosmosOptions),
            _mockLogger.Object);

        // Act
        var result = await repository.GetByIdAsync("nonexistent");

        // Assert
        result.Should().BeNull();
        attemptCount.Should().Be(1); // No retries for NotFound

        // Verify no retry warnings were logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cosmos DB operation failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task SaveAsync_GeneratesIdIfNotProvided()
    {
        // Arrange
        var mockClient = new Mock<CosmosClient>();
        var mockContainer = new Mock<Container>();
        var testData = TestHelpers.CreateTestData(null!, "test-value"); // No ID provided

        mockClient.Setup(x => x.GetContainer(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(mockContainer.Object);

        mockContainer.Setup(x => x.UpsertItemAsync(
                It.IsAny<CachedData>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((CachedData data, PartitionKey pk, ItemRequestOptions opts, CancellationToken ct) =>
                CreateMockItemResponse(data));

        var repository = new CosmosDbDataRepository(
            mockClient.Object,
            Options.Create(_cosmosOptions),
            _mockLogger.Object);

        // Act
        var result = await repository.SaveAsync(testData);

        // Assert
        result.Should().NotBeNullOrEmpty();
        Guid.TryParse(result, out _).Should().BeTrue(); // Should be a valid GUID
    }

    [Fact]
    public async Task GetByIdAsync_LogsSuccessfulRetrieval()
    {
        // Arrange
        var mockClient = new Mock<CosmosClient>();
        var mockContainer = new Mock<Container>();
        var testData = TestHelpers.CreateTestData("test-id", "test-value");

        mockClient.Setup(x => x.GetContainer(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(mockContainer.Object);

        mockContainer.Setup(x => x.ReadItemAsync<CachedData>(
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMockItemResponse(testData));

        var repository = new CosmosDbDataRepository(
            mockClient.Object,
            Options.Create(_cosmosOptions),
            _mockLogger.Object);

        // Act
        await repository.GetByIdAsync("test-id");

        // Assert - Verify debug log was written
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Retrieved item")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private static ItemResponse<CachedData> CreateMockItemResponse(CachedData data)
    {
        var mockResponse = new Mock<ItemResponse<CachedData>>();
        mockResponse.Setup(x => x.Resource).Returns(data);
        mockResponse.Setup(x => x.RequestCharge).Returns(1.0);
        return mockResponse.Object;
    }
}
