// ============================================================================
// File: ITableRunner.cs
// Author: Codex
// Project: ReportSyncer
// Description: Contract for executing sync operations for a single table.
// ============================================================================

using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Core.Sync;

/// <summary>
/// Executes delete/insert operations for a single table using a provided execution context.
/// </summary>
public interface ITableRunner
{
    /// <summary>
    /// Runs the sync pipeline for a single table.
    /// </summary>
    /// <param name="ctx">Context describing the job, tables, filters, and mapping.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="TableResult"/> summarizing the table execution.</returns>
    Task<TableResult> RunAsync(TableExecutionContext ctx, CancellationToken ct);

    /// <summary>
    /// Executes a single phase (Delete or Insert) for a table.
    /// </summary>
    /// <param name="ctx">Context describing the job, tables, filters, and mapping.</param>
    /// <param name="phase">Phase to execute.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="TableResult"/> for the executed phase.</returns>
    Task<TableResult> RunPhaseAsync(TableExecutionContext ctx, SyncPhase phase, CancellationToken ct);
}
