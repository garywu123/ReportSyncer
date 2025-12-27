// ============================================================================
// File: WorkEstimate.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: DTO representing estimated work for a table sync.
// ============================================================================

namespace ReportSyncer.Core.Observability;

/// <summary>
/// Represents an estimation of rows to delete and insert during sync.
/// </summary>
/// <param name="EstimatedRowsToDelete">Rows expected to be deleted on the target.</param>
/// <param name="EstimatedRowsToInsert">Rows expected to be inserted into the target.</param>
/// <param name="EstimatedDeletePct">Optional percent of target rows expected to be deleted.</param>
/// <param name="DeleteStats">Optional additional delete statistics.</param>
/// <param name="Warnings">Warnings produced while estimating.</param>
public sealed record WorkEstimate(
    long EstimatedRowsToDelete,
    long EstimatedRowsToInsert,
    double? EstimatedDeletePct,
    EstimatedDeleteStats? DeleteStats,
    IReadOnlyList<string> Warnings);
