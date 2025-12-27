// ============================================================================
// File: EstimatedDeleteStats.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: Delete statistics produced by work estimation.
// ============================================================================

namespace ReportSyncer.Core.Observability;

/// <summary>
/// Represents detailed delete estimation statistics.
/// </summary>
/// <param name="TargetTotalRows">Total rows in the target table.</param>
/// <param name="RowsToDelete">Rows expected to be deleted.</param>
/// <param name="DeletePct">Optional percent of target rows expected to be deleted.</param>
public sealed record EstimatedDeleteStats(
    long TargetTotalRows,
    long RowsToDelete,
    double? DeletePct);
