// ============================================================================
// File: ProgressLogThrottlerTests.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-04
// Description: Tests for ProgressLogThrottler.
// ============================================================================

using ReportSyncer.Console.Logging;
using Xunit;

namespace ReportSyncer.Console.Tests.Logging;

public sealed class ProgressLogThrottlerTests
{
    [Fact]
    public void Constructor_WithValidParameters_Succeeds()
    {
        // Arrange
        var timeProvider = TimeProvider.System;
        var interval = TimeSpan.FromSeconds(5);

        // Act
        var throttler = new ProgressLogThrottler(timeProvider, interval);

        // Assert
        Assert.NotNull(throttler);
        Assert.Equal(0, throttler.TrackedKeyCount);
    }

    [Fact]
    public void Constructor_WithNullTimeProvider_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new ProgressLogThrottler(null!, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Constructor_WithZeroInterval_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => 
            new ProgressLogThrottler(TimeProvider.System, TimeSpan.Zero));
        Assert.Contains("positive", ex.Message);
    }

    [Fact]
    public void ShouldWrite_FirstCall_ReturnsTrue()
    {
        // Arrange
        var throttler = new ProgressLogThrottler(TimeProvider.System, TimeSpan.FromSeconds(5));

        // Act
        var result = throttler.ShouldWrite("job1", "table1", "InProgress", isTerminal: false);

        // Assert
        Assert.True(result);
        Assert.Equal(1, throttler.TrackedKeyCount);
    }

    [Fact]
    public void ShouldWrite_WithinInterval_ReturnsFalse()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var throttler = new ProgressLogThrottler(fakeTime, TimeSpan.FromSeconds(5));

        // Act
        var result1 = throttler.ShouldWrite("job1", "table1", "InProgress", isTerminal: false);
        fakeTime.Advance(TimeSpan.FromSeconds(2)); // Within interval
        var result2 = throttler.ShouldWrite("job1", "table1", "InProgress", isTerminal: false);

        // Assert
        Assert.True(result1);
        Assert.False(result2); // Throttled
    }

    [Fact]
    public void ShouldWrite_AfterInterval_ReturnsTrue()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var throttler = new ProgressLogThrottler(fakeTime, TimeSpan.FromSeconds(5));

        // Act
        var result1 = throttler.ShouldWrite("job1", "table1", "InProgress", isTerminal: false);
        fakeTime.Advance(TimeSpan.FromSeconds(6)); // Beyond interval
        var result2 = throttler.ShouldWrite("job1", "table1", "InProgress", isTerminal: false);

        // Assert
        Assert.True(result1);
        Assert.True(result2); // Not throttled
    }

    [Fact]
    public void ShouldWrite_TerminalEvent_AlwaysReturnsTrue()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var throttler = new ProgressLogThrottler(fakeTime, TimeSpan.FromSeconds(5));

        // Act
        var result1 = throttler.ShouldWrite("job1", "table1", "InProgress", isTerminal: false);
        fakeTime.Advance(TimeSpan.FromSeconds(1)); // Within interval
        var result2 = throttler.ShouldWrite("job1", "table1", "Completed", isTerminal: true);

        // Assert
        Assert.True(result1);
        Assert.True(result2); // Terminal bypasses throttling
    }

    [Fact]
    public void ShouldWrite_TerminalEvent_UpdatesTimestamp()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var throttler = new ProgressLogThrottler(fakeTime, TimeSpan.FromSeconds(5));

        // Act
        throttler.ShouldWrite("job1", "table1", "InProgress", isTerminal: false);
        fakeTime.Advance(TimeSpan.FromSeconds(2));
        throttler.ShouldWrite("job1", "table1", "Completed", isTerminal: true); // Updates timestamp
        fakeTime.Advance(TimeSpan.FromSeconds(2)); // Total 4 seconds from initial
        var result = throttler.ShouldWrite("job1", "table1", "InProgress", isTerminal: false);

        // Assert
        Assert.False(result); // Should be throttled (only 2s since terminal event)
    }

    [Fact]
    public void ShouldWrite_DifferentKeys_TrackedIndependently()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var throttler = new ProgressLogThrottler(fakeTime, TimeSpan.FromSeconds(5));

        // Act
        var result1 = throttler.ShouldWrite("job1", "table1", "InProgress", isTerminal: false);
        var result2 = throttler.ShouldWrite("job1", "table2", "InProgress", isTerminal: false);
        var result3 = throttler.ShouldWrite("job2", "table1", "InProgress", isTerminal: false);

        // Assert
        Assert.True(result1);
        Assert.True(result2);
        Assert.True(result3);
        Assert.Equal(3, throttler.TrackedKeyCount);
    }

    [Fact]
    public void ShouldWrite_SameKeyDifferentPhase_TrackedSeparately()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var throttler = new ProgressLogThrottler(fakeTime, TimeSpan.FromSeconds(5));

        // Act
        var result1 = throttler.ShouldWrite("job1", "table1", "Schema", isTerminal: false);
        var result2 = throttler.ShouldWrite("job1", "table1", "Data", isTerminal: false);

        // Assert
        Assert.True(result1);
        Assert.True(result2);
        Assert.Equal(2, throttler.TrackedKeyCount);
    }

    [Fact]
    public void ShouldWrite_WithNullOrEmptyFields_ReturnsTrue()
    {
        // Arrange
        var throttler = new ProgressLogThrottler(TimeProvider.System, TimeSpan.FromSeconds(5));

        // Act & Assert
        Assert.True(throttler.ShouldWrite(null!, "table", "phase", false));
        Assert.True(throttler.ShouldWrite("job", "", "phase", false));
        Assert.True(throttler.ShouldWrite("job", "table", null!, false));
        Assert.Equal(0, throttler.TrackedKeyCount); // Invalid keys not tracked
    }

    // Helper class for testing with controllable time
    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _currentTime;

        public FakeTimeProvider(DateTimeOffset startTime)
        {
            _currentTime = startTime;
        }

        public override DateTimeOffset GetUtcNow() => _currentTime;

        public void Advance(TimeSpan duration)
        {
            _currentTime += duration;
        }
    }
}
