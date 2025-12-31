// ============================================================================
// File: ProgressEventsEnhancementTests.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-01
// Description: Tests for Section 6 progress events enhancement (ProgressMetrics).
// ============================================================================

using FluentAssertions;
using ReportSyncer.Core.Observability;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Sync.Contracts;
using Xunit;

namespace ReportSyncer.Core.Tests.Observability;

/// <summary>
/// Tests for Section 6 enhancement: ProgressMetrics and InProgress events.
/// </summary>
/// <remarks>
/// These tests verify:
/// 1. Backward compatibility: Section 5 usage patterns continue to work
/// 2. New capabilities: ProgressMetrics can be used with InProgress events
/// 3. Field validation: New optional fields work correctly
/// </remarks>
public class ProgressEventsEnhancementTests
{
    #region Section 5 Backward Compatibility Tests

    [Fact]
    public void TableProgressEvent_WithoutNewFields_CreatesSuccessfully()
    {
        // Arrange & Act: Section 5 usage pattern
        var evt = new TableProgressEvent(
            "job-123",
            TableIdentifier.Parse("dbo.Orders"),
            ProgressEventKind.Started,
            SyncPhase.Insert,
            DateTimeOffset.UtcNow
        );
        
        // Assert: Basic properties work
        evt.JobId.Should().Be("job-123");
        evt.Kind.Should().Be(ProgressEventKind.Started);
        evt.Table.ToString().Should().Be("dbo.Orders");
        evt.Phase.Should().Be(SyncPhase.Insert);
        
        // Assert: New fields should be null (backward compatible)
        evt.Metrics.Should().BeNull();
    }

    [Fact]
    public void JobProgressEvent_WithoutNewFields_CreatesSuccessfully()
    {
        // Arrange & Act: Section 5 usage pattern
        var evt = new JobProgressEvent(
            "job-123",
            ProgressEventKind.Started,
            ProgressPhase.Execution,
            DateTimeOffset.UtcNow
        );
        
        // Assert: Basic properties work
        evt.JobId.Should().Be("job-123");
        evt.Kind.Should().Be(ProgressEventKind.Started);
        evt.Phase.Should().Be(ProgressPhase.Execution);
        
        // Assert: New fields should be null (backward compatible)
        evt.Metrics.Should().BeNull();
    }

    #endregion

    #region Section 6 Enhancement Tests

    [Fact]
    public void ProgressEventKind_HasInProgressValue()
    {
        // Assert: InProgress enum value exists
        var inProgress = ProgressEventKind.InProgress;
        inProgress.Should().BeDefined();
    }

    [Fact]
    public void ProgressMetrics_CanBeCreatedWithAllFields()
    {
        // Arrange & Act
        var metrics = new ProgressMetrics(
            RowsProcessed: 5000,
            TotalRowsPlanned: 10000,
            PercentComplete: 50.0,
            ThroughputRowsPerSec: 250.5,
            EtaSeconds: 20
        );
        
        // Assert
        metrics.RowsProcessed.Should().Be(5000);
        metrics.TotalRowsPlanned.Should().Be(10000);
        metrics.PercentComplete.Should().Be(50.0);
        metrics.ThroughputRowsPerSec.Should().Be(250.5);
        metrics.EtaSeconds.Should().Be(20);
    }

    [Fact]
    public void ProgressMetrics_SupportsNullableFields()
    {
        // Arrange & Act: Create metrics with only RowsProcessed
        var metrics = new ProgressMetrics(
            RowsProcessed: 1000,
            TotalRowsPlanned: null,
            PercentComplete: null,
            ThroughputRowsPerSec: null,
            EtaSeconds: null
        );
        
        // Assert: Nullable fields are null
        metrics.RowsProcessed.Should().Be(1000);
        metrics.TotalRowsPlanned.Should().BeNull();
        metrics.PercentComplete.Should().BeNull();
        metrics.ThroughputRowsPerSec.Should().BeNull();
        metrics.EtaSeconds.Should().BeNull();
    }

    [Fact]
    public void TableProgressEvent_WithProgressMetrics_PopulatesMetricsField()
    {
        // Arrange
        var metrics = new ProgressMetrics(
            RowsProcessed: 5000,
            TotalRowsPlanned: 10000,
            PercentComplete: 50.0,
            ThroughputRowsPerSec: 250.5,
            EtaSeconds: 20
        );
        
        // Act: Section 6 enhancement usage
        var evt = new TableProgressEvent(
            "job-123",
            TableIdentifier.Parse("dbo.Orders"),
            ProgressEventKind.InProgress,
            SyncPhase.Insert,
            DateTimeOffset.UtcNow,
            Metrics: metrics
        );
        
        // Assert: Event properties
        evt.Kind.Should().Be(ProgressEventKind.InProgress);
        evt.JobId.Should().Be("job-123");
        evt.Table.ToString().Should().Be("dbo.Orders");
        
        // Assert: Metrics are populated
        evt.Metrics.Should().NotBeNull();
        evt.Metrics!.RowsProcessed.Should().Be(5000);
        evt.Metrics.TotalRowsPlanned.Should().Be(10000);
        evt.Metrics.PercentComplete.Should().Be(50.0);
        evt.Metrics.ThroughputRowsPerSec.Should().Be(250.5);
        evt.Metrics.EtaSeconds.Should().Be(20);
    }

    [Fact]
    public void JobProgressEvent_WithProgressMetrics_PopulatesMetricsField()
    {
        // Arrange: Aggregated metrics across all tables
        var metrics = new ProgressMetrics(
            RowsProcessed: 15000,
            TotalRowsPlanned: 30000,
            PercentComplete: 50.0,
            ThroughputRowsPerSec: 500.0,
            EtaSeconds: 30
        );
        
        // Act: Section 6 enhancement usage
        var evt = new JobProgressEvent(
            "job-123",
            ProgressEventKind.InProgress,
            ProgressPhase.Execution,
            DateTimeOffset.UtcNow,
            Metrics: metrics
        );
        
        // Assert: Event properties
        evt.Kind.Should().Be(ProgressEventKind.InProgress);
        evt.Phase.Should().Be(ProgressPhase.Execution);
        
        // Assert: Metrics are populated
        evt.Metrics.Should().NotBeNull();
        evt.Metrics!.RowsProcessed.Should().Be(15000);
        evt.Metrics.TotalRowsPlanned.Should().Be(30000);
        evt.Metrics.PercentComplete.Should().Be(50.0);
    }

    #endregion

    #region Lifecycle Event Tests

    [Fact]
    public void TableProgressEvent_CompletedWithRowsAffected_WorksCorrectly()
    {
        // Arrange & Act: Completed event uses RowsAffected, not Metrics
        var evt = new TableProgressEvent(
            "job-123",
            TableIdentifier.Parse("dbo.Orders"),
            ProgressEventKind.Completed,
            SyncPhase.Insert,
            DateTimeOffset.UtcNow,
            RowsAffected: 10000
        );
        
        // Assert: RowsAffected is set, Metrics is null
        evt.Kind.Should().Be(ProgressEventKind.Completed);
        evt.RowsAffected.Should().Be(10000);
        evt.Metrics.Should().BeNull();
    }

    [Fact]
    public void TableProgressEvent_InProgressWithMetrics_DoesNotUseRowsAffected()
    {
        // Arrange
        var metrics = new ProgressMetrics(5000, 10000, 50.0, 250.5, 20);
        
        // Act: InProgress event uses Metrics, not RowsAffected
        var evt = new TableProgressEvent(
            "job-123",
            TableIdentifier.Parse("dbo.Orders"),
            ProgressEventKind.InProgress,
            SyncPhase.Insert,
            DateTimeOffset.UtcNow,
            Metrics: metrics
        );
        
        // Assert: Metrics is set, RowsAffected is null
        evt.Kind.Should().Be(ProgressEventKind.InProgress);
        evt.Metrics.Should().NotBeNull();
        evt.RowsAffected.Should().BeNull();
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void ProgressMetrics_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var metrics1 = new ProgressMetrics(5000, 10000, 50.0, 250.5, 20);
        var metrics2 = new ProgressMetrics(5000, 10000, 50.0, 250.5, 20);
        var metrics3 = new ProgressMetrics(6000, 10000, 60.0, 250.5, 15);
        
        // Assert: Structural equality
        metrics1.Should().Be(metrics2);
        metrics1.Should().NotBe(metrics3);
    }

    [Fact]
    public void TableProgressEvent_WithSameMetrics_AreEqual()
    {
        // Arrange
        var metrics = new ProgressMetrics(5000, 10000, 50.0, 250.5, 20);
        var now = DateTimeOffset.UtcNow;
        
        var evt1 = new TableProgressEvent(
            "job-123",
            TableIdentifier.Parse("dbo.Orders"),
            ProgressEventKind.InProgress,
            SyncPhase.Insert,
            now,
            Metrics: metrics
        );
        
        var evt2 = new TableProgressEvent(
            "job-123",
            TableIdentifier.Parse("dbo.Orders"),
            ProgressEventKind.InProgress,
            SyncPhase.Insert,
            now,
            Metrics: metrics
        );
        
        // Assert: Structural equality
        evt1.Should().Be(evt2);
    }

    #endregion
}
