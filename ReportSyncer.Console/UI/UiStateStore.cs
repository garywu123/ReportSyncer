// ============================================================================
// File: UiStateStore.cs
// Author: Gary Wu
// Project: ReportSyncer.Console
// Date: January 5, 2026
// Description: UI state store that maintains table set invariant from ExecutionPlan.
// ============================================================================

using Microsoft.Extensions.Logging;
using ReportSyncer.Core.Observability;
using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Console.UI;

/// <summary>
/// Maintains UI state for all tables. Table set is immutable (from ExecutionPlan).
/// Events can only update status, never add or remove tables.
/// </summary>
/// <remarks>
/// CRITICAL RULE: Table set is initialized from ExecutionPlan and never changes.
/// Unknown tables in events are logged and ignored.
/// </remarks>
public sealed class UiStateStore
{
    private readonly Dictionary<string, int> _tableNameToIndex;
    private readonly List<TableRowState> _rows;
    private readonly ILogger<UiStateStore> _logger;
    private readonly object _lock = new();

    /// <summary>
    /// Gets a read-only snapshot of all table rows.
    /// </summary>
    public IReadOnlyList<TableRowState> AllRows
    {
        get
        {
            lock (_lock)
            {
                return _rows.ToList();
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of <see cref="UiStateStore"/>.
    /// </summary>
    /// <param name="executionPlanTables">Tables from ExecutionPlan (schema.table format).</param>
    /// <param name="logger">Logger for warnings.</param>
    public UiStateStore(IReadOnlyList<string> executionPlanTables, ILogger<UiStateStore> logger)
    {
        if (executionPlanTables is null)
            throw new ArgumentNullException(nameof(executionPlanTables));

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _tableNameToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        _rows = new List<TableRowState>(executionPlanTables.Count);

        for (int i = 0; i < executionPlanTables.Count; i++)
        {
            var tableName = executionPlanTables[i];
            _tableNameToIndex[tableName] = i;
            _rows.Add(new TableRowState(
                TableName: tableName,
                InsertOrder: i,
                Status: TableStatus.Planned,
                Phase: "Planned",
                Remarks: null));
        }
    }

    /// <summary>
    /// Applies a UI event to update state.
    /// </summary>
    /// <param name="evt">Event to apply.</param>
    public void Apply(UiEvent evt)
    {
        if (evt is null)
            return;

        switch (evt.Kind)
        {
            case UiEventKind.Table:
                ApplyTableEvent(evt);
                break;

            case UiEventKind.Job:
                // Job-level events don't update table rows in this minimal implementation
                break;
        }
    }

    /// <summary>
    /// Gets summary counts for Area B display.
    /// </summary>
    public AreaBSummary GetSummary()
    {
        lock (_lock)
        {
            int running = 0, completed = 0, failed = 0, skipped = 0;

            foreach (var row in _rows)
            {
                switch (row.Status)
                {
                    case TableStatus.Planned:
                        // Not yet started - don't count as running
                        break;
                    case TableStatus.Succeeded:
                        completed++;
                        break;
                    case TableStatus.FailedPreFlight:
                    case TableStatus.FailedExecution:
                        failed++;
                        break;
                    case TableStatus.SkippedDryRun:
                        skipped++;
                        break;
                    case TableStatus.Cancelled:
                        skipped++;
                        break;
                }
            }

            return new AreaBSummary(_rows.Count, running, completed, failed, skipped);
        }
    }

    private void ApplyTableEvent(UiEvent evt)
    {
        if (evt.Payload is not TableProgressEvent tableEvt)
        {
            _logger.LogWarning("UiStateStore received Table event with invalid payload type");
            return;
        }

        var tableName = tableEvt.Table.ToString();

        lock (_lock)
        {
            if (!_tableNameToIndex.TryGetValue(tableName, out int index))
            {
                _logger.LogWarning("Received event for unknown table: {TableName}. Table not in ExecutionPlan", tableName);
                return;
            }

            var existing = _rows[index];
            var newStatus = MapEventKindToStatus(tableEvt.Kind, existing.Status);
            var newPhase = tableEvt.Phase.ToString();
            var newRemarks = BuildRemarks(tableEvt);

            _rows[index] = existing with
            {
                Status = newStatus,
                Phase = newPhase,
                Remarks = newRemarks
            };
        }
    }

    private static TableStatus MapEventKindToStatus(ProgressEventKind kind, TableStatus currentStatus)
    {
        return kind switch
        {
            ProgressEventKind.Started => currentStatus, // Keep current status, just update phase
            ProgressEventKind.InProgress => currentStatus,
            ProgressEventKind.Completed => TableStatus.Succeeded,
            ProgressEventKind.Failed => TableStatus.FailedExecution,
            ProgressEventKind.Skipped => TableStatus.SkippedDryRun,
            _ => currentStatus
        };
    }

    private static string? BuildRemarks(TableProgressEvent evt)
    {
        if (!string.IsNullOrWhiteSpace(evt.ErrorMessage))
            return evt.ErrorMessage;

        if (!string.IsNullOrWhiteSpace(evt.Message))
            return evt.Message;

        if (evt.RowsAffected.HasValue)
            return $"{evt.RowsAffected} rows";

        if (evt.Metrics is not null)
            return $"{evt.Metrics.RowsProcessed}/{evt.Metrics.TotalRowsPlanned}";

        return null;
    }
}
