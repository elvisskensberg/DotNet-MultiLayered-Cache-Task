using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Moq;
using MultiLayeredCache.Application.Features.CreateData;
using MultiLayeredCache.Application.Features.GetData;
using MultiLayeredCache.Domain.Exceptions;
using MultiLayeredCache.Domain.Interfaces;
using MultiLayeredCache.Domain.Models;

namespace MultiLayeredCache.Api.Tests;

/// <summary>
/// Integration tests for the Data API endpoints
/// Tests the multi-layered caching behavior using mocked data provider
/// </summary>
public class DataControllerIntegrationTests
{
    private readonly Mock<IDataProvider> _mockDataProvider;
    private readonly Mock<IDataRepository> _mockRepository;
    private readonly HttpClient _client;

    public DataControllerIntegrationTests()
    {
        // Setup shared mocks
        _mockDataProvider = new Mock<IDataProvider>();
        _mockRepository = new Mock<IDataRepository>();

        // Create test client
        var factory = new TestWebApplicationFactory(_mockDataProvider.Object, _mockRepository.Object);
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostData_WithValidRequest_ReturnsCreatedWithId()
    {
        // Arrange
        var expectedId = Guid.NewGuid().ToString();
        var testValue = $"test-value-{Guid.NewGuid()}";

        _mockRepository
            .Setup(x => x.SaveAsync(It.IsAny<CachedData>()))
            .ReturnsAsync(expectedId);

        var command = new CreateDataCommand(testValue);

        // Act
        var response = await _client.PostAsJsonAsync("/data", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        result.Should().NotBeNull();
        result!["id"].Should().Be(expectedId);

        _mockRepository.Verify(x => x.SaveAsync(It.Is<CachedData>(d => d.Value == testValue)), Times.Once);
    }

    [Fact]
    public async Task PostData_WithEmptyValue_ReturnsBadRequest()
    {
        // Arrange
        var command = new CreateDataCommand("");

        // Act
        var response = await _client.PostAsJsonAsync("/data", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _mockRepository.Verify(x => x.SaveAsync(It.IsAny<CachedData>()), Times.Never);
    }

    [Fact]
    public async Task PostData_WithLongValue_ReturnsBadRequest()
    {
        // Arrange
        var command = new CreateDataCommand(new string('a', 1001)); // Exceeds max length

        // Act
        var response = await _client.PostAsJsonAsync("/data", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _mockRepository.Verify(x => x.SaveAsync(It.IsAny<CachedData>()), Times.Never);
    }

    [Fact]
    public async Task GetData_WhenFoundInCache_ReturnsData()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString();
        var testValue = $"test-value-{Guid.NewGuid()}";
        var cachedData = TestHelpers.CreateTestData(testId, testValue);

        _mockDataProvider
            .Setup(x => x.GetByIdAsync(testId))
            .ReturnsAsync(cachedData);

        // Act
        var response = await _client.GetAsync($"/data/{testId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<GetDataResponse>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(testId);
        result.Value.Should().Be(testValue);

        _mockDataProvider.Verify(x => x.GetByIdAsync(testId), Times.Once);
    }

    [Fact]
    public async Task GetData_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString();

        _mockDataProvider
            .Setup(x => x.GetByIdAsync(testId))
            .ThrowsAsync(new NotFoundException($"Data with ID '{testId}' not found in database"));

        // Act
        var response = await _client.GetAsync($"/data/{testId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _mockDataProvider.Verify(x => x.GetByIdAsync(testId), Times.Once);
    }

    [Fact]
    public async Task GetData_WithWhitespaceId_ReturnsBadRequest()
    {
        // Arrange & Act - Using URL encoded space
        var response = await _client.GetAsync("/data/%20");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostAndGetData_EndToEndScenario_WorksCorrectly()
    {
        // Arrange
        var generatedId = Guid.NewGuid().ToString();
        var testValue = $"test-value-{Guid.NewGuid()}";
        var command = new CreateDataCommand(testValue);

        _mockRepository
            .Setup(x => x.SaveAsync(It.IsAny<CachedData>()))
            .ReturnsAsync(generatedId);

        // Act 1: POST data
        var postResponse = await _client.PostAsJsonAsync("/data", command);
        postResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var postResult = await postResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var createdId = postResult!["id"];

        // Setup for GET request
        var savedData = TestHelpers.CreateTestData(createdId, testValue);
        _mockDataProvider.Setup(x => x.GetByIdAsync(createdId))
            .ReturnsAsync(savedData);

        // Act 2: GET the data
        var getResponse = await _client.GetAsync($"/data/{createdId}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getResult = await getResponse.Content.ReadFromJsonAsync<GetDataResponse>();
        getResult.Should().NotBeNull();
        getResult!.Id.Should().Be(createdId);
        getResult.Value.Should().Be(testValue);

        _mockRepository.Verify(x => x.SaveAsync(It.IsAny<CachedData>()), Times.Once);
        _mockDataProvider.Verify(x => x.GetByIdAsync(createdId), Times.Once);
    }

    [Fact]
    public async Task GetData_MultipleRequests_UsesDataProviderCorrectly()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString();
        var data = TestHelpers.CreateTestData(testId);

        _mockDataProvider.Setup(x => x.GetByIdAsync(testId))
            .ReturnsAsync(data);

        // Act - Multiple requests
        var response1 = await _client.GetAsync($"/data/{testId}");
        var response2 = await _client.GetAsync($"/data/{testId}");

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);

        _mockDataProvider.Verify(x => x.GetByIdAsync(testId), Times.Exactly(2));
    }
}
