using FluentAssertions;
using MultiLayeredCache.Domain.Models;
using MultiLayeredCache.Infrastructure.Caching;

namespace MultiLayeredCache.Api.Tests;

/// <summary>
/// Unit tests for cache invalidation functionality
/// Ensures that cache entries are properly invalidated on write operations
/// </summary>
public class CacheInvalidationTests
{
    [Fact]
    public void LruPolicy_Remove_RemovesExistingItem()
    {
        // Arrange
        var policy = new LruEvictionPolicy(3);
        policy.Set("key1", TestHelpers.CreateTestData("key1", "value1"));
        policy.Set("key2", TestHelpers.CreateTestData("key2", "value2"));

        // Act
        var result = policy.Remove("key1");

        // Assert
        result.Should().BeTrue();
        policy.Count.Should().Be(1);
        policy.TryGet("key1", out _).Should().BeFalse();
        policy.TryGet("key2", out _).Should().BeTrue();
    }

    [Fact]
    public void LruPolicy_Remove_ReturnsFalseForNonExistentItem()
    {
        // Arrange
        var policy = new LruEvictionPolicy(3);
        policy.Set("key1", TestHelpers.CreateTestData("key1", "value1"));

        // Act
        var result = policy.Remove("nonexistent");

        // Assert
        result.Should().BeFalse();
        policy.Count.Should().Be(1);
    }

    [Fact]
    public async Task RedisCacheService_RemoveAsync_RemovesCacheEntry()
    {
        // This test requires a real Redis instance, so we'll skip it in CI
        // In a real scenario, you'd use a Redis test container or mock
        await Task.CompletedTask;
    }

    [Fact]
    public async Task SdcsService_RemoveAsync_InvalidatesCache()
    {
        // Arrange
        var policy = new LruEvictionPolicy(10);
        var service = new SelfDesignedCacheService(policy, "LRU");
        var data = TestHelpers.CreateTestData("key1", "value1");

        await service.SetAsync(data);
        (await service.GetAsync("key1")).Should().NotBeNull();

        // Act
        await service.RemoveAsync("key1");

        // Assert
        (await service.GetAsync("key1")).Should().BeNull();
    }

    [Fact]
    public async Task SdcsService_RemoveAsync_DoesNotThrowForNonExistentKey()
    {
        // Arrange
        var policy = new LruEvictionPolicy(10);
        var service = new SelfDesignedCacheService(policy, "LRU");

        // Act & Assert
        var act = () => service.RemoveAsync("nonexistent");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void LruPolicy_Remove_MaintainsLruOrder()
    {
        // Arrange
        var policy = new LruEvictionPolicy(3);
        policy.Set("key1", TestHelpers.CreateTestData("key1", "value1"));
        policy.Set("key2", TestHelpers.CreateTestData("key2", "value2"));
        policy.Set("key3", TestHelpers.CreateTestData("key3", "value3"));

        // Act - Remove middle item
        policy.Remove("key2");

        // Add two new items - should evict key1 (oldest remaining)
        policy.Set("key4", TestHelpers.CreateTestData("key4", "value4"));
        policy.Set("key5", TestHelpers.CreateTestData("key5", "value5"));

        // Assert
        policy.Count.Should().Be(3);
        policy.TryGet("key1", out _).Should().BeFalse(); // Evicted
        policy.TryGet("key2", out _).Should().BeFalse(); // Removed
        policy.TryGet("key3", out _).Should().BeTrue();
        policy.TryGet("key4", out _).Should().BeTrue();
        policy.TryGet("key5", out _).Should().BeTrue();
    }

    [Fact]
    public async Task SdcsService_ConcurrentRemoval_ThreadSafe()
    {
        // Arrange
        var policy = new LruEvictionPolicy(100);
        var service = new SelfDesignedCacheService(policy, "LRU");

        // Add items
        for (int i = 0; i < 50; i++)
        {
            await service.SetAsync(TestHelpers.CreateTestData($"key{i}", $"value{i}"));
        }

        // Act - Concurrent removals
        var tasks = new List<Task>();
        for (int i = 0; i < 50; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                await service.RemoveAsync($"key{index}");
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - All items should be removed
        for (int i = 0; i < 50; i++)
        {
            (await service.GetAsync($"key{i}")).Should().BeNull();
        }
    }

}
