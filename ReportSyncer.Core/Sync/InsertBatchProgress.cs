// ============================================================================
// File: InsertBatchProgress.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: January 1, 2026
// Description: Progress notification for batch insert operations.
//              Used by IDataWriter to report progress to TableRunner.
// ============================================================================

namespace ReportSyncer.Core.Sync;

/// <summary>
/// Represents progress information for a batch insert operation.
/// Used by <see cref="IDataWriter"/> implementations to report progress
/// during execution of <see cref="IDataWriter.InsertAsync"/>.
/// </summary>
/// <param name="TotalProcessed">
/// Total number of rows processed so far (cumulative across all batches).
/// </param>
/// <param name="TotalPlanned">
/// Total number of rows planned for insertion in the entire operation.
/// </param>
/// <param name="BatchRowsAffected">
/// Number of rows affected in the most recently completed batch.
/// </param>
/// <remarks>
/// This record enables progress tracking without coupling the data writer
/// to progress calculation logic. The data writer simply reports raw numbers;
/// the caller (e.g., <see cref="TableRunner"/>) is responsible for calculating
/// metrics like throughput, percentage, and ETA using <see cref="Observability.ProgressTracker"/>.
/// </remarks>
public sealed record InsertBatchProgress(
    long TotalProcessed,
    long TotalPlanned,
    int BatchRowsAffected);
