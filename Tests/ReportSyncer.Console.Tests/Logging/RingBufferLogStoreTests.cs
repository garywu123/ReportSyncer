// ============================================================================
// File: RingBufferLogStoreTests.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-04
// Description: Tests for RingBufferLogStore.
// ============================================================================

using ReportSyncer.Console.Logging;
using Xunit;

namespace ReportSyncer.Console.Tests.Logging;

public sealed class RingBufferLogStoreTests
{
    [Fact]
    public void Constructor_WithPositiveCapacity_Succeeds()
    {
        // Act
        var store = new RingBufferLogStore(10);

        // Assert
        Assert.Equal(10, store.Capacity);
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Constructor_WithZeroCapacity_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => new RingBufferLogStore(0));
        Assert.Contains("positive", ex.Message);
    }

    [Fact]
    public void Constructor_WithNegativeCapacity_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => new RingBufferLogStore(-5));
        Assert.Contains("positive", ex.Message);
    }

    [Fact]
    public void Add_WithValidLine_IncreasesCount()
    {
        // Arrange
        var store = new RingBufferLogStore(5);

        // Act
        store.Add("Line 1");
        store.Add("Line 2");

        // Assert
        Assert.Equal(2, store.Count);
    }

    [Fact]
    public void Add_WithNullLine_DoesNotIncrease()
    {
        // Arrange
        var store = new RingBufferLogStore(5);

        // Act
        store.Add(null!);

        // Assert
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Add_WithEmptyLine_DoesNotIncrease()
    {
        // Arrange
        var store = new RingBufferLogStore(5);

        // Act
        store.Add("");
        store.Add("   ");

        // Assert
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Add_ExceedingCapacity_DiscardsOldest()
    {
        // Arrange
        var store = new RingBufferLogStore(3);

        // Act
        store.Add("Line 1");
        store.Add("Line 2");
        store.Add("Line 3");
        store.Add("Line 4");
        store.Add("Line 5");

        // Assert
        Assert.Equal(3, store.Count);
        var snapshot = store.Snapshot();
        Assert.Equal(new[] { "Line 3", "Line 4", "Line 5" }, snapshot);
    }

    [Fact]
    public void Snapshot_ReturnsOldestToNewestOrder()
    {
        // Arrange
        var store = new RingBufferLogStore(5);
        store.Add("First");
        store.Add("Second");
        store.Add("Third");

        // Act
        var snapshot = store.Snapshot();

        // Assert
        Assert.Equal(new[] { "First", "Second", "Third" }, snapshot);
    }

    [Fact]
    public void Snapshot_ReturnsCopy_NotAffectedBySubsequentAdds()
    {
        // Arrange
        var store = new RingBufferLogStore(5);
        store.Add("Line 1");
        store.Add("Line 2");

        // Act
        var snapshot1 = store.Snapshot();
        store.Add("Line 3");
        var snapshot2 = store.Snapshot();

        // Assert
        Assert.Equal(2, snapshot1.Count);
        Assert.Equal(3, snapshot2.Count);
    }

    [Fact]
    public async Task Add_Concurrent_MaintainsThreadSafety()
    {
        // Arrange
        var store = new RingBufferLogStore(100);
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 10; i++)
        {
            var taskIndex = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 20; j++)
                {
                    store.Add($"Task {taskIndex} - Line {j}");
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(100, store.Count); // Should be capped at capacity
        var snapshot = store.Snapshot();
        Assert.Equal(100, snapshot.Count);
        Assert.All(snapshot, line => Assert.NotNull(line));
    }

    [Fact]
    public void Snapshot_Empty_ReturnsEmptyList()
    {
        // Arrange
        var store = new RingBufferLogStore(5);

        // Act
        var snapshot = store.Snapshot();

        // Assert
        Assert.Empty(snapshot);
    }

    [Fact]
    public void Add_AtExactCapacity_DoesNotExceedCapacity()
    {
        // Arrange
        var store = new RingBufferLogStore(3);

        // Act
        store.Add("Line 1");
        store.Add("Line 2");
        store.Add("Line 3");

        // Assert
        Assert.Equal(3, store.Count);
        var snapshot = store.Snapshot();
        Assert.Equal(new[] { "Line 1", "Line 2", "Line 3" }, snapshot);
    }
}
