// ============================================================================
// File: RunLogJobProgressReporterTests.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: January 1, 2026
// Description: Unit tests for RunLogJobProgressReporter event mapping and correlation.
// ============================================================================

using FluentAssertions;
using Moq;
using ReportSyncer.Core.Observability;
using ReportSyncer.Core.Observability.RunLog;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Sync.Contracts;
using Xunit;

namespace ReportSyncer.Core.Tests.Observability.RunLog;

public sealed class RunLogJobProgressReporterTests
{
    [Fact]
    public void Report_WithJobProgressEvent_CallsWriterAppendJob()
    {
        // Arrange
        var mockWriter = new Mock<IRunLogWriter>();
        var reporter = new RunLogJobProgressReporter(mockWriter.Object);

        var evt = new JobProgressEvent(
            JobId: "test-job",
            Kind: ProgressEventKind.Started,
            Phase: ProgressPhase.Execution,
            UtcTimestamp: DateTimeOffset.UtcNow
        );

        // Act
        reporter.Report(evt);

        // Give async operation time to complete
        Thread.Sleep(100);

        // Assert
        mockWriter.Verify(w => w.AppendJobAsync(
            It.Is<RunLogEntry>(e => e.JobId == "test-job" && e.EventKind == "Started"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Report_WithTableProgressEvent_CallsWriterAppendTable()
    {
        // Arrange
        var mockWriter = new Mock<IRunLogWriter>();
        var reporter = new RunLogJobProgressReporter(mockWriter.Object);

        var evt = new TableProgressEvent(
            JobId: "test-job",
            Table: TableIdentifier.Parse("dbo.Users"),
            Kind: ProgressEventKind.Completed,
            Phase: SyncPhase.Insert,
            UtcTimestamp: DateTimeOffset.UtcNow
        );

        // Act
        reporter.Report(evt);

        // Give async operation time to complete
        Thread.Sleep(100);

        // Assert
        mockWriter.Verify(w => w.AppendTableAsync(
            It.Is<RunLogEntry>(e => e.JobId == "test-job" && e.Table == "dbo.Users"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(ProgressEventKind.Started, "Info")]
    [InlineData(ProgressEventKind.Completed, "Info")]
    [InlineData(ProgressEventKind.InProgress, "Debug")]
    [InlineData(ProgressEventKind.Skipped, "Warn")]
    [InlineData(ProgressEventKind.Failed, "Error")]
    public void Report_WithJobEvent_MapsEventKindToCorrectLevel(ProgressEventKind kind, string expectedLevel)
    {
        // Arrange
        var mockWriter = new Mock<IRunLogWriter>();
        var reporter = new RunLogJobProgressReporter(mockWriter.Object);

        var evt = new JobProgressEvent(
            JobId: "test-job",
            Kind: kind,
            Phase: ProgressPhase.Execution,
            UtcTimestamp: DateTimeOffset.UtcNow
        );

        // Act
        reporter.Report(evt);

        // Give async operation time to complete
        Thread.Sleep(100);

        // Assert
        mockWriter.Verify(w => w.AppendJobAsync(
            It.Is<RunLogEntry>(e => e.Level == expectedLevel),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Report_WithJobEventMissingJobId_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockWriter = new Mock<IRunLogWriter>();
        var reporter = new RunLogJobProgressReporter(mockWriter.Object);

        var evt = new JobProgressEvent(
            JobId: "",
            Kind: ProgressEventKind.Started,
            Phase: ProgressPhase.Execution,
            UtcTimestamp: DateTimeOffset.UtcNow
        );

        // Act
        var act = () => reporter.Report(evt);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*JobId*");
    }

    [Fact]
    public void Report_WithTableEventMissingJobId_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockWriter = new Mock<IRunLogWriter>();
        var reporter = new RunLogJobProgressReporter(mockWriter.Object);

        var evt = new TableProgressEvent(
            JobId: "",
            Table: TableIdentifier.Parse("dbo.Users"),
            Kind: ProgressEventKind.Started,
            Phase: SyncPhase.Insert,
            UtcTimestamp: DateTimeOffset.UtcNow
        );

        // Act
        var act = () => reporter.Report(evt);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*JobId*");
    }

    [Fact]
    public void Report_WithJobEventContainingMetrics_MapsMetricsCorrectly()
    {
        // Arrange
        var mockWriter = new Mock<IRunLogWriter>();
        var reporter = new RunLogJobProgressReporter(mockWriter.Object);

        var metrics = new ProgressMetrics(
            RowsProcessed: 1000,
            TotalRowsPlanned: 2000,
            PercentComplete: 50.0,
            ThroughputRowsPerSec: 200.5,
            EtaSeconds: 10
        );

        var evt = new JobProgressEvent(
            JobId: "test-job",
            Kind: ProgressEventKind.InProgress,
            Phase: ProgressPhase.Execution,
            UtcTimestamp: DateTimeOffset.UtcNow,
            Metrics: metrics
        );

        // Act
        reporter.Report(evt);

        // Give async operation time to complete
        Thread.Sleep(100);

        // Assert
        mockWriter.Verify(w => w.AppendJobAsync(
            It.Is<RunLogEntry>(e =>
                e.Metrics != null &&
                e.Metrics.RowsProcessed == 1000 &&
                e.Metrics.TotalRowsPlanned == 2000 &&
                e.Metrics.PercentComplete == 50.0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Report_WithTableEventContainingRowsAffected_MapsRowsAffected()
    {
        // Arrange
        var mockWriter = new Mock<IRunLogWriter>();
        var reporter = new RunLogJobProgressReporter(mockWriter.Object);

        var evt = new TableProgressEvent(
            JobId: "test-job",
            Table: TableIdentifier.Parse("dbo.Orders"),
            Kind: ProgressEventKind.Completed,
            Phase: SyncPhase.Insert,
            UtcTimestamp: DateTimeOffset.UtcNow,
            RowsAffected: 42
        );

        // Act
        reporter.Report(evt);

        // Give async operation time to complete
        Thread.Sleep(100);

        // Assert
        mockWriter.Verify(w => w.AppendTableAsync(
            It.Is<RunLogEntry>(e => e.RowsAffected == 42),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Report_WithInnerReporter_ChainsToInner()
    {
        // Arrange
        var mockWriter = new Mock<IRunLogWriter>();
        var mockInner = new Mock<IJobProgressReporter>();
        var reporter = new RunLogJobProgressReporter(mockWriter.Object, mockInner.Object);

        var evt = new JobProgressEvent(
            JobId: "test-job",
            Kind: ProgressEventKind.Started,
            Phase: ProgressPhase.Execution,
            UtcTimestamp: DateTimeOffset.UtcNow
        );

        // Act
        reporter.Report(evt);

        // Give async operation time to complete
        Thread.Sleep(100);

        // Assert
        mockInner.Verify(i => i.Report(evt), Times.Once);
    }

    [Fact]
    public void Report_WithFailedEvent_IncludesErrorInformation()
    {
        // Arrange
        var mockWriter = new Mock<IRunLogWriter>();
        var reporter = new RunLogJobProgressReporter(mockWriter.Object);

        var evt = new JobProgressEvent(
            JobId: "test-job",
            Kind: ProgressEventKind.Failed,
            Phase: ProgressPhase.Execution,
            UtcTimestamp: DateTimeOffset.UtcNow,
            ErrorCode: "ERR001",
            ErrorMessage: "Database connection failed"
        );

        // Act
        reporter.Report(evt);

        // Give async operation time to complete
        Thread.Sleep(100);

        // Assert
        mockWriter.Verify(w => w.AppendJobAsync(
            It.Is<RunLogEntry>(e =>
                e.Level == "Error" &&
                e.ErrorCode == "ERR001" &&
                e.ErrorMessage == "Database connection failed"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Report_WithElapsedTime_MapsToElapsedMs()
    {
        // Arrange
        var mockWriter = new Mock<IRunLogWriter>();
        var reporter = new RunLogJobProgressReporter(mockWriter.Object);

        var evt = new JobProgressEvent(
            JobId: "test-job",
            Kind: ProgressEventKind.Completed,
            Phase: ProgressPhase.Execution,
            UtcTimestamp: DateTimeOffset.UtcNow,
            Elapsed: TimeSpan.FromSeconds(5.5)
        );

        // Act
        reporter.Report(evt);

        // Give async operation time to complete
        Thread.Sleep(100);

        // Assert
        mockWriter.Verify(w => w.AppendJobAsync(
            It.Is<RunLogEntry>(e => e.ElapsedMs == 5500),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Report_WithDryRunTableEvent_SetsDryRunFlag()
    {
        // Arrange
        var mockWriter = new Mock<IRunLogWriter>();
        var reporter = new RunLogJobProgressReporter(mockWriter.Object);

        var evt = new TableProgressEvent(
            JobId: "test-job",
            Table: TableIdentifier.Parse("dbo.Products"),
            Kind: ProgressEventKind.Completed,
            Phase: SyncPhase.Delete,
            UtcTimestamp: DateTimeOffset.UtcNow,
            IsDryRun: true
        );

        // Act
        reporter.Report(evt);

        // Give async operation time to complete
        Thread.Sleep(100);

        // Assert
        mockWriter.Verify(w => w.AppendTableAsync(
            It.Is<RunLogEntry>(e => e.IsDryRun == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
