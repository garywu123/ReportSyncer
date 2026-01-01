// ============================================================================
// File: IDataWriter.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: Abstraction for executing delete and insert operations during sync.
// ============================================================================

using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Core.Sync;

/// <summary>
/// Executes data modification operations for a table sync.
/// </summary>
public interface IDataWriter
{
    /// <summary>
    /// Executes a delete operation for the provided table context.
    /// </summary>
    /// <param name="ctx">Table execution context describing the target and filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Total rows deleted.</returns>
    Task<int> DeleteAsync(TableExecutionContext ctx, CancellationToken ct);

    /// <summary>
    /// Executes batched inserts for the provided table context.
    /// </summary>
    /// <param name="ctx">Table execution context describing the target and mapping.</param>
    /// <param name="rows">Rows to insert, keyed by target column name (case-insensitive).</param>
    /// <param name="progress">Optional progress reporter for batch completion notifications.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Total rows inserted.</returns>
    Task<int> InsertAsync(
        TableExecutionContext ctx,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        IProgress<InsertBatchProgress>? progress,
        CancellationToken ct);
}
