using FluentAssertions;
using MultiLayeredCache.Domain.Models;
using MultiLayeredCache.Infrastructure.Caching;

namespace MultiLayeredCache.Api.Tests;

/// <summary>
/// Unit tests for LRU (Least Recently Used) eviction policy
/// Tests O(1) operations, eviction behavior, and edge cases
/// </summary>
public class EvictionPolicyTests
{
    #region LRU Policy Tests

    [Fact]
    public void LruPolicy_TryGet_WhenKeyExists_ReturnsTrue()
    {
        // Arrange
        var policy = new LruEvictionPolicy(3);
        var data = TestHelpers.CreateTestData("key1", "value1");
        policy.Set("key1", data);

        // Act
        var result = policy.TryGet("key1", out var value);

        // Assert
        result.Should().BeTrue();
        value.Should().NotBeNull();
        value!.Id.Should().Be("key1");
        value.Value.Should().Be("value1");
    }

    [Fact]
    public void LruPolicy_TryGet_WhenKeyDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var policy = new LruEvictionPolicy(3);

        // Act
        var result = policy.TryGet("nonexistent", out var value);

        // Assert
        result.Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void LruPolicy_Set_WhenAtCapacity_EvictsLeastRecentlyUsed()
    {
        // Arrange
        var policy = new LruEvictionPolicy(3);
        policy.Set("key1", TestHelpers.CreateTestData("key1", "value1"));
        policy.Set("key2", TestHelpers.CreateTestData("key2", "value2"));
        policy.Set("key3", TestHelpers.CreateTestData("key3", "value3"));

        // Act - Add 4th item, should evict key1 (least recently used)
        policy.Set("key4", TestHelpers.CreateTestData("key4", "value4"));

        // Assert
        policy.Count.Should().Be(3);
        policy.TryGet("key1", out _).Should().BeFalse(); // key1 evicted
        policy.TryGet("key2", out _).Should().BeTrue();
        policy.TryGet("key3", out _).Should().BeTrue();
        policy.TryGet("key4", out _).Should().BeTrue();
    }

    [Fact]
    public void LruPolicy_Set_WhenAccessingItem_MovesToFront()
    {
        // Arrange
        var policy = new LruEvictionPolicy(3);
        policy.Set("key1", TestHelpers.CreateTestData("key1", "value1"));
        policy.Set("key2", TestHelpers.CreateTestData("key2", "value2"));
        policy.Set("key3", TestHelpers.CreateTestData("key3", "value3"));

        // Act - Access key1, making it most recently used
        policy.TryGet("key1", out _);

        // Add 4th item, should evict key2 (now least recently used)
        policy.Set("key4", TestHelpers.CreateTestData("key4", "value4"));

        // Assert
        policy.TryGet("key1", out _).Should().BeTrue(); // key1 still present
        policy.TryGet("key2", out _).Should().BeFalse(); // key2 evicted
        policy.TryGet("key3", out _).Should().BeTrue();
        policy.TryGet("key4", out _).Should().BeTrue();
    }

    [Fact]
    public void LruPolicy_Set_UpdatesExistingKey()
    {
        // Arrange
        var policy = new LruEvictionPolicy(3);
        policy.Set("key1", TestHelpers.CreateTestData("key1", "value1"));

        // Act
        policy.Set("key1", TestHelpers.CreateTestData("key1", "value1_updated"));

        // Assert
        policy.Count.Should().Be(1);
        policy.TryGet("key1", out var value).Should().BeTrue();
        value!.Value.Should().Be("value1_updated");
    }

    [Fact]
    public void LruPolicy_Clear_RemovesAllItems()
    {
        // Arrange
        var policy = new LruEvictionPolicy(3);
        policy.Set("key1", TestHelpers.CreateTestData("key1", "value1"));
        policy.Set("key2", TestHelpers.CreateTestData("key2", "value2"));

        // Act
        policy.Clear();

        // Assert
        policy.Count.Should().Be(0);
        policy.TryGet("key1", out _).Should().BeFalse();
        policy.TryGet("key2", out _).Should().BeFalse();
    }

    [Fact]
    public void LruPolicy_Constructor_WithInvalidCapacity_ThrowsException()
    {
        // Act & Assert
        var act = () => new LruEvictionPolicy(0);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Capacity must be at least 1*");
    }

    #endregion

    #region Performance and Capacity Tests

    [Theory]
    [InlineData(3)]
    [InlineData(10)]
    [InlineData(100)]
    public void LruPolicy_RespectsCapacity(int capacity)
    {
        // Arrange
        var policy = new LruEvictionPolicy(capacity);

        // Act - Add more items than capacity
        for (int i = 0; i < capacity + 10; i++)
        {
            policy.Set($"key{i}", TestHelpers.CreateTestData($"key{i}", $"value{i}"));
        }

        // Assert
        policy.Count.Should().BeLessThanOrEqualTo(capacity);
    }

    #endregion

}
