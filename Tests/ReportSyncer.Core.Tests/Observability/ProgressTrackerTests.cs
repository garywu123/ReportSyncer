// ============================================================================
// File: ProgressTrackerTests.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: January 1, 2026
// Description: Unit tests for ProgressTracker progress calculation and throttling.
// ============================================================================

using FluentAssertions;
using ReportSyncer.Core.Observability;
using Xunit;

namespace ReportSyncer.Core.Tests.Observability;

public sealed class ProgressTrackerTests
{
    [Fact]
    public void Constructor_WithNegativeTotalPlanned_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => new ProgressTracker(
            totalPlanned: -1,
            emitInterval: TimeSpan.FromSeconds(1),
            etaSmoothing: null);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("totalPlanned");
    }

    [Fact]
    public void Constructor_WithNonPositiveEmitInterval_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => new ProgressTracker(
            totalPlanned: 100,
            emitInterval: TimeSpan.Zero,
            etaSmoothing: null);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("emitInterval");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Constructor_WithInvalidEtaSmoothing_ThrowsArgumentOutOfRangeException(double smoothing)
    {
        // Act
        var act = () => new ProgressTracker(
            totalPlanned: 100,
            emitInterval: TimeSpan.FromSeconds(1),
            etaSmoothing: smoothing);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("etaSmoothing");
    }

    [Fact]
    public void AddProgress_WithNegativeDelta_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var tracker = CreateTracker(totalPlanned: 100);

        // Act
        var act = () => tracker.AddProgress(-1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("deltaRows");
    }

    [Fact]
    public void TryBuildMetrics_BeforeEmitInterval_ReturnsFalse()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow;
        var tracker = new ProgressTracker(
            totalPlanned: 100,
            emitInterval: TimeSpan.FromSeconds(1),
            etaSmoothing: null,
            utcNowProvider: () => currentTime);

        // Act - try to build metrics immediately after adding progress
        tracker.AddProgress(50);
        currentTime = currentTime.AddMilliseconds(500); // Only 0.5s elapsed
        var result = tracker.TryBuildMetrics(out var metrics);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void TryBuildMetrics_AfterEmitInterval_ReturnsTrue()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow;
        var tracker = new ProgressTracker(
            totalPlanned: 100,
            emitInterval: TimeSpan.FromSeconds(1),
            etaSmoothing: null,
            utcNowProvider: () => currentTime);

        // Act
        tracker.AddProgress(50);
        currentTime = currentTime.AddSeconds(1.5); // 1.5s elapsed
        var result = tracker.TryBuildMetrics(out var metrics);

        // Assert
        result.Should().BeTrue();
        metrics.Should().NotBeNull();
        metrics.TotalRowsPlanned.Should().Be(100);
        metrics.RowsProcessed.Should().Be(50);
    }

    [Fact]
    public void TryBuildMetrics_CalculatesPercentCorrectly()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow;
        var tracker = new ProgressTracker(
            totalPlanned: 100,
            emitInterval: TimeSpan.FromSeconds(1),
            etaSmoothing: null,
            utcNowProvider: () => currentTime);

        // Act
        tracker.AddProgress(50);
        currentTime = currentTime.AddSeconds(1);
        tracker.TryBuildMetrics(out var metrics);

        // Assert
        metrics.PercentComplete.Should().BeApproximately(50.0, 0.1);
    }

    [Fact]
    public void TryBuildMetrics_WithZeroTotalPlanned_PercentIsNull()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow;
        var tracker = new ProgressTracker(
            totalPlanned: 0,
            emitInterval: TimeSpan.FromSeconds(1),
            etaSmoothing: null,
            utcNowProvider: () => currentTime);

        // Act
        currentTime = currentTime.AddSeconds(1);
        tracker.TryBuildMetrics(out var metrics);

        // Assert
        metrics.PercentComplete.Should().BeNull();
    }

    [Fact]
    public void TryBuildMetrics_WithNoSmoothing_UsesOverallAverageThroughput()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow;
        var tracker = new ProgressTracker(
            totalPlanned: 100,
            emitInterval: TimeSpan.FromSeconds(1),
            etaSmoothing: null, // No smoothing
            utcNowProvider: () => currentTime);

        // Act - process 50 rows over 10 seconds
        tracker.AddProgress(50);
        currentTime = currentTime.AddSeconds(10);
        tracker.TryBuildMetrics(out var metrics);

        // Assert - overall average = 50 / 10 = 5 rows/sec
        metrics.ThroughputRowsPerSec.Should().BeApproximately(5.0, 0.1);
    }

    [Fact]
    public void TryBuildMetrics_WithSmoothingZero_UsesOnlyRecentThroughput()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow;
        var tracker = new ProgressTracker(
            totalPlanned: 200,
            emitInterval: TimeSpan.FromSeconds(1),
            etaSmoothing: 0.0, // No weight to recent samples
            utcNowProvider: () => currentTime);

        // Act - first emit: 50 rows in 10s (5 rows/sec)
        tracker.AddProgress(50);
        currentTime = currentTime.AddSeconds(10);
        tracker.TryBuildMetrics(out var metrics1);

        // Act - second emit: another 100 rows in 10s (10 rows/sec instant)
        tracker.AddProgress(100);
        currentTime = currentTime.AddSeconds(10);
        tracker.TryBuildMetrics(out var metrics2);

        // Assert - with alpha=0, EMA = old value (5.0)
        metrics2.ThroughputRowsPerSec.Should().BeApproximately(5.0, 0.1);
    }

    [Fact]
    public void TryBuildMetrics_WithSmoothingOne_UsesOnlyCurrentThroughput()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow;
        var tracker = new ProgressTracker(
            totalPlanned: 200,
            emitInterval: TimeSpan.FromSeconds(1),
            etaSmoothing: 1.0, // Full weight to recent samples
            utcNowProvider: () => currentTime);

        // Act - first emit: 50 rows in 10s (5 rows/sec)
        tracker.AddProgress(50);
        currentTime = currentTime.AddSeconds(10);
        tracker.TryBuildMetrics(out var metrics1);

        // Act - second emit: another 100 rows in 10s (10 rows/sec instant)
        tracker.AddProgress(100);
        currentTime = currentTime.AddSeconds(10);
        tracker.TryBuildMetrics(out var metrics2);

        // Assert - with alpha=1, EMA = instant value (10.0)
        metrics2.ThroughputRowsPerSec.Should().BeApproximately(10.0, 0.1);
    }

    [Fact]
    public void TryBuildMetrics_WithSmoothingHalf_InterpolatesThroughput()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow;
        var tracker = new ProgressTracker(
            totalPlanned: 200,
            emitInterval: TimeSpan.FromSeconds(1),
            etaSmoothing: 0.5, // Half weight to recent
            utcNowProvider: () => currentTime);

        // Act - first emit: 50 rows in 10s (5 rows/sec)
        tracker.AddProgress(50);
        currentTime = currentTime.AddSeconds(10);
        tracker.TryBuildMetrics(out var metrics1);

        // Act - second emit: another 100 rows in 10s (10 rows/sec instant)
        tracker.AddProgress(100);
        currentTime = currentTime.AddSeconds(10);
        tracker.TryBuildMetrics(out var metrics2);

        // Assert - with alpha=0.5, EMA = 0.5*10 + 0.5*5 = 7.5
        metrics2.ThroughputRowsPerSec.Should().BeApproximately(7.5, 0.1);
    }

    [Fact]
    public void TryBuildMetrics_CalculatesEtaCorrectly()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow;
        var tracker = new ProgressTracker(
            totalPlanned: 100,
            emitInterval: TimeSpan.FromSeconds(1),
            etaSmoothing: null,
            utcNowProvider: () => currentTime);

        // Act - 50 rows in 10 seconds = 5 rows/sec, remaining 50 rows = 10 seconds
        tracker.AddProgress(50);
        currentTime = currentTime.AddSeconds(10);
        tracker.TryBuildMetrics(out var metrics);

        // Assert
        metrics.EtaSeconds.Should().Be(10);
    }

    [Fact]
    public void TryBuildMetrics_WhenComplete_EtaIsZero()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow;
        var tracker = new ProgressTracker(
            totalPlanned: 100,
            emitInterval: TimeSpan.FromSeconds(1),
            etaSmoothing: null,
            utcNowProvider: () => currentTime);

        // Act - all rows processed
        tracker.AddProgress(100);
        currentTime = currentTime.AddSeconds(10);
        tracker.TryBuildMetrics(out var metrics);

        // Assert
        metrics.EtaSeconds.Should().Be(0);
        metrics.PercentComplete.Should().BeApproximately(100.0, 0.1);
    }

    [Fact]
    public void AddProgress_CapsAtTotalPlanned()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow;
        var tracker = new ProgressTracker(
            totalPlanned: 100,
            emitInterval: TimeSpan.FromSeconds(1),
            etaSmoothing: null,
            utcNowProvider: () => currentTime);

        // Act - add more than total planned
        tracker.AddProgress(150);
        currentTime = currentTime.AddSeconds(1);
        tracker.TryBuildMetrics(out var metrics);

        // Assert - capped at 100
        metrics.RowsProcessed.Should().Be(100);
        metrics.PercentComplete.Should().BeApproximately(100.0, 0.1);
    }

    [Fact]
    public void TryBuildMetrics_MultipleThrottledCalls_OnlyFirstAfterIntervalSucceeds()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow;
        var tracker = new ProgressTracker(
            totalPlanned: 100,
            emitInterval: TimeSpan.FromSeconds(1),
            etaSmoothing: null,
            utcNowProvider: () => currentTime);

        // Act
        tracker.AddProgress(10);
        currentTime = currentTime.AddSeconds(1.5);
        var result1 = tracker.TryBuildMetrics(out _);

        // Try immediately again
        var result2 = tracker.TryBuildMetrics(out _);

        // Wait another second
        currentTime = currentTime.AddSeconds(1);
        var result3 = tracker.TryBuildMetrics(out _);

        // Assert
        result1.Should().BeTrue(); // First call after interval succeeds
        result2.Should().BeFalse(); // Throttled
        result3.Should().BeTrue(); // After another interval succeeds
    }

    private static ProgressTracker CreateTracker(long totalPlanned = 100, double? etaSmoothing = null)
    {
        return new ProgressTracker(
            totalPlanned: totalPlanned,
            emitInterval: TimeSpan.FromSeconds(1),
            etaSmoothing: etaSmoothing);
    }
}
