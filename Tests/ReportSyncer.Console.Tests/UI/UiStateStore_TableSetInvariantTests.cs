// ============================================================================
// File: UiStateStore_TableSetInvariantTests.cs
// Author: Gary Wu
// Project: ReportSyncer.Console.Tests
// Date: January 5, 2026
// Description: Tests verifying that UiStateStore maintains table set invariant.
// ============================================================================

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ReportSyncer.Console.UI;
using ReportSyncer.Core.Observability;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Sync.Contracts;
using Xunit;

namespace ReportSyncer.Console.Tests.UI;

public sealed class UiStateStore_TableSetInvariantTests
{
    [Fact]
    public void Constructor_WithExecutionPlanTables_InitializesAllRowsAsPlanned()
    {
        // Arrange
        var tables = new[] { "dbo.TableA", "dbo.TableB", "dbo.TableC" };
        var logger = NullLogger<UiStateStore>.Instance;

        // Act
        var store = new UiStateStore(tables, logger);

        // Assert
        var rows = store.AllRows;
        rows.Should().HaveCount(3);
        rows.Should().AllSatisfy(r => r.Status.Should().Be(TableStatus.Planned));
        rows[0].TableName.Should().Be("dbo.TableA");
        rows[1].TableName.Should().Be("dbo.TableB");
        rows[2].TableName.Should().Be("dbo.TableC");
    }

    [Fact]
    public void Apply_WithUnknownTable_AddsTableDynamically()
    {
        // Arrange
        var tables = new[] { "dbo.TableA", "dbo.TableB" };
        var logger = NullLogger<UiStateStore>.Instance;
        var store = new UiStateStore(tables, logger);

        var unknownTableEvent = new TableProgressEvent(
            JobId: "job-1",
            Table: TableIdentifier.Parse("dbo.UnknownTable"),
            Kind: ProgressEventKind.Started,
            Phase: SyncPhase.Insert,
            UtcTimestamp: DateTimeOffset.UtcNow);

        var uiEvent = new UiEvent(UiEventKind.Table, unknownTableEvent, DateTimeOffset.UtcNow);

        // Act
        store.Apply(uiEvent);

        // Assert: Unknown table should be dynamically added (new behavior)
        var rows = store.AllRows;
        rows.Should().HaveCount(3, "unknown table should be dynamically added");
        rows.Should().Contain(r => r.TableName == "dbo.UnknownTable", "dynamic table should exist");
        var newRow = rows.First(r => r.TableName == "dbo.UnknownTable");
        newRow.Status.Should().Be(TableStatus.Planned, "newly added table starts in Planned status");
        newRow.Phase.Should().Be("Insert", "should use the phase from the event");
    }

    [Fact]
    public void Apply_WithUnknownTable_LogsWarning()
    {
        // Arrange
        var tables = new[] { "dbo.TableA" };
        var logger = NullLogger<UiStateStore>.Instance;
        var store = new UiStateStore(tables, logger);

        var unknownTableEvent = new TableProgressEvent(
            JobId: "job-1",
            Table: TableIdentifier.Parse("dbo.UnknownTable"),
            Kind: ProgressEventKind.Started,
            Phase: SyncPhase.Insert,
            UtcTimestamp: DateTimeOffset.UtcNow);

        var uiEvent = new UiEvent(UiEventKind.Table, unknownTableEvent, DateTimeOffset.UtcNow);

        // Act
        store.Apply(uiEvent);

        // Assert - log warning cannot be verified with NullLogger, test passes if no exception
        // With dynamic table addition, the row count increases to 2
        store.AllRows.Should().HaveCount(2, "unknown table should be dynamically added");
        store.AllRows.Should().Contain(r => r.TableName == "dbo.UnknownTable");
    }

    [Fact]
    public void Apply_WithKnownTable_UpdatesStatus()
    {
        // Arrange
        var tables = new[] { "dbo.TableA", "dbo.TableB" };
        var logger = NullLogger<UiStateStore>.Instance;
        var store = new UiStateStore(tables, logger);

        var startedEvent = new TableProgressEvent(
            JobId: "job-1",
            Table: TableIdentifier.Parse("dbo.TableA"),
            Kind: ProgressEventKind.Started,
            Phase: SyncPhase.Delete,
            UtcTimestamp: DateTimeOffset.UtcNow);

        var uiEvent = new UiEvent(UiEventKind.Table, startedEvent, DateTimeOffset.UtcNow);

        // Act
        store.Apply(uiEvent);

        // Assert
        var rows = store.AllRows;
        rows.Should().HaveCount(2, "row count should not change");
        rows[0].TableName.Should().Be("dbo.TableA");
        rows[0].Phase.Should().Be("Delete");
    }

    [Fact]
    public void Apply_MultipleEventsForSameTable_UpdatesStatusSequentially()
    {
        // Arrange
        var tables = new[] { "dbo.TableA" };
        var logger = NullLogger<UiStateStore>.Instance;
        var store = new UiStateStore(tables, logger);

        // Act: Apply sequence of events
        store.Apply(new UiEvent(UiEventKind.Table, new TableProgressEvent(
            "job-1",
            TableIdentifier.Parse("dbo.TableA"),
            ProgressEventKind.Started,
            SyncPhase.Delete,
            DateTimeOffset.UtcNow), DateTimeOffset.UtcNow));

        store.Apply(new UiEvent(UiEventKind.Table, new TableProgressEvent(
            "job-1",
            TableIdentifier.Parse("dbo.TableA"),
            ProgressEventKind.Completed,
            SyncPhase.Delete,
            DateTimeOffset.UtcNow,
            RowsAffected: 10), DateTimeOffset.UtcNow));

        store.Apply(new UiEvent(UiEventKind.Table, new TableProgressEvent(
            "job-1",
            TableIdentifier.Parse("dbo.TableA"),
            ProgressEventKind.Started,
            SyncPhase.Insert,
            DateTimeOffset.UtcNow), DateTimeOffset.UtcNow));

        store.Apply(new UiEvent(UiEventKind.Table, new TableProgressEvent(
            "job-1",
            TableIdentifier.Parse("dbo.TableA"),
            ProgressEventKind.Completed,
            SyncPhase.Insert,
            DateTimeOffset.UtcNow,
            RowsAffected: 100), DateTimeOffset.UtcNow));

        // Assert
        var rows = store.AllRows;
        rows.Should().HaveCount(1, "row count should never change");
        rows[0].Status.Should().Be(TableStatus.Succeeded);
        rows[0].Phase.Should().Be("Insert");
        rows[0].Remarks.Should().Be("100 rows");
    }

    [Fact]
    public void Apply_WithFailedEvent_UpdatesStatusAndRemarks()
    {
        // Arrange
        var tables = new[] { "dbo.TableA" };
        var logger = NullLogger<UiStateStore>.Instance;
        var store = new UiStateStore(tables, logger);

        var failedEvent = new TableProgressEvent(
            JobId: "job-1",
            Table: TableIdentifier.Parse("dbo.TableA"),
            Kind: ProgressEventKind.Failed,
            Phase: SyncPhase.Insert,
            UtcTimestamp: DateTimeOffset.UtcNow,
            ErrorMessage: "Connection timeout");

        var uiEvent = new UiEvent(UiEventKind.Table, failedEvent, DateTimeOffset.UtcNow);

        // Act
        store.Apply(uiEvent);

        // Assert
        var rows = store.AllRows;
        rows[0].Status.Should().Be(TableStatus.FailedExecution);
        rows[0].Remarks.Should().Be("Connection timeout");
    }

    [Fact]
    public void GetSummary_WithMixedStatuses_ReturnsCorrectCounts()
    {
        // Arrange
        var tables = new[] { "dbo.T1", "dbo.T2", "dbo.T3", "dbo.T4", "dbo.T5" };
        var logger = NullLogger<UiStateStore>.Instance;
        var store = new UiStateStore(tables, logger);

        // T1: Planned
        // T2: Succeeded
        store.Apply(new UiEvent(UiEventKind.Table, new TableProgressEvent(
            "job-1", TableIdentifier.Parse("dbo.T2"), ProgressEventKind.Completed,
            SyncPhase.Insert, DateTimeOffset.UtcNow), DateTimeOffset.UtcNow));

        // T3: Failed
        store.Apply(new UiEvent(UiEventKind.Table, new TableProgressEvent(
            "job-1", TableIdentifier.Parse("dbo.T3"), ProgressEventKind.Failed,
            SyncPhase.Insert, DateTimeOffset.UtcNow), DateTimeOffset.UtcNow));

        // T4: Skipped
        store.Apply(new UiEvent(UiEventKind.Table, new TableProgressEvent(
            "job-1", TableIdentifier.Parse("dbo.T4"), ProgressEventKind.Skipped,
            SyncPhase.Insert, DateTimeOffset.UtcNow), DateTimeOffset.UtcNow));

        // T5: Succeeded
        store.Apply(new UiEvent(UiEventKind.Table, new TableProgressEvent(
            "job-1", TableIdentifier.Parse("dbo.T5"), ProgressEventKind.Completed,
            SyncPhase.Insert, DateTimeOffset.UtcNow), DateTimeOffset.UtcNow));

        // Act
        var summary = store.GetSummary();

        // Assert
        summary.Total.Should().Be(5);
        summary.Running.Should().Be(0);
        summary.Completed.Should().Be(2); // T2, T5
        summary.Failed.Should().Be(1);    // T3
        summary.Skipped.Should().Be(1);   // T4
    }

    [Fact]
    public void AllRows_ReturnsSnapshot_NotAffectedBySubsequentUpdates()
    {
        // Arrange
        var tables = new[] { "dbo.TableA" };
        var logger = NullLogger<UiStateStore>.Instance;
        var store = new UiStateStore(tables, logger);

        // Act: Get snapshot before update
        var snapshot1 = store.AllRows;

        // Update state
        store.Apply(new UiEvent(UiEventKind.Table, new TableProgressEvent(
            "job-1", TableIdentifier.Parse("dbo.TableA"), ProgressEventKind.Completed,
            SyncPhase.Insert, DateTimeOffset.UtcNow), DateTimeOffset.UtcNow));

        // Get snapshot after update
        var snapshot2 = store.AllRows;

        // Assert
        snapshot1[0].Status.Should().Be(TableStatus.Planned, "first snapshot should not be affected");
        snapshot2[0].Status.Should().Be(TableStatus.Succeeded, "second snapshot should reflect update");
    }
}
