// ============================================================================
// File: PreflightGateResult.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: Result of running the preflight gate across all tables.
// ============================================================================

namespace ReportSyncer.Core.Preflight;

/// <summary>
/// Aggregated result from evaluating the preflight gate.
/// </summary>
/// <param name="IsAllowed">True when all table reports are allowed.</param>
/// <param name="TableReports">Per-table gate reports.</param>
/// <param name="BlockingCodes">Deduplicated blocking safety codes.</param>
public sealed record PreflightGateResult(
    bool IsAllowed,
    IReadOnlyList<TablePreflightGateReport> TableReports,
    IReadOnlyList<string> BlockingCodes);
