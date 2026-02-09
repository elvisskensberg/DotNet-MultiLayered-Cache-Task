using FluentAssertions;
using MultiLayeredCache.Domain.Models;
using MultiLayeredCache.Infrastructure.Caching;

namespace MultiLayeredCache.Api.Tests;

/// <summary>
/// Unit tests for SelfDesignedCacheService with real eviction policies
/// Tests the service layer wrapping the eviction policies
/// </summary>
public class SdcsServiceTests
{
    [Fact]
    public async Task SdcsService_WithLruPolicy_GetAsync_ReturnsNullWhenEmpty()
    {
        // Arrange
        var policy = new LruEvictionPolicy(10);
        var service = new SelfDesignedCacheService(policy, "LRU");

        // Act
        var result = await service.GetAsync("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SdcsService_WithLruPolicy_SetAndGet_WorksCorrectly()
    {
        // Arrange
        var policy = new LruEvictionPolicy(10);
        var service = new SelfDesignedCacheService(policy, "LRU");
        var data = TestHelpers.CreateTestData("key1", "value1");

        // Act
        await service.SetAsync(data);
        var result = await service.GetAsync("key1");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("key1");
        result.Value.Should().Be("value1");
    }

    [Fact]
    public async Task SdcsService_WithLruPolicy_LayerName_IncludesStrategy()
    {
        // Arrange
        var policy = new LruEvictionPolicy(10);
        var service = new SelfDesignedCacheService(policy, "LRU");

        // Act
        var layerName = service.LayerName;

        // Assert
        layerName.Should().Contain("LRU");
        layerName.Should().Contain("SDCS");
    }

    [Fact]
    public async Task SdcsService_WithLruPolicy_RespectsEvictionBehavior()
    {
        // Arrange
        var policy = new LruEvictionPolicy(3);
        var service = new SelfDesignedCacheService(policy, "LRU");

        // Add 3 items
        await service.SetAsync(TestHelpers.CreateTestData("key1", "value1"));
        await service.SetAsync(TestHelpers.CreateTestData("key2", "value2"));
        await service.SetAsync(TestHelpers.CreateTestData("key3", "value3"));

        // Act - Add 4th item, should evict key1 (LRU)
        await service.SetAsync(TestHelpers.CreateTestData("key4", "value4"));

        // Assert
        (await service.GetAsync("key1")).Should().BeNull(); // Evicted
        (await service.GetAsync("key2")).Should().NotBeNull();
        (await service.GetAsync("key3")).Should().NotBeNull();
        (await service.GetAsync("key4")).Should().NotBeNull();
    }

    [Fact]
    public void SdcsService_WithNullPolicy_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new SelfDesignedCacheService(null!, "LRU");
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("evictionPolicy");
    }

    [Fact]
    public async Task SdcsService_ConcurrentAccess_ThreadSafe()
    {
        // Arrange
        var policy = new LruEvictionPolicy(100);
        var service = new SelfDesignedCacheService(policy, "LRU");
        var tasks = new List<Task>();

        // Act - Concurrent writes
        for (int i = 0; i < 50; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                await service.SetAsync(TestHelpers.CreateTestData($"key{index}", $"value{index}"));
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - All writes should succeed
        tasks.Clear();
        for (int i = 0; i < 50; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                var result = await service.GetAsync($"key{index}");
                result.Should().NotBeNull();
            }));
        }

        await Task.WhenAll(tasks);
    }

}
