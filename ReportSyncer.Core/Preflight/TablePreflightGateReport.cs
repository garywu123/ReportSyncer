// ============================================================================
// File: TablePreflightGateReport.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: Per-table report emitted by the preflight gate.
// ============================================================================

using ReportSyncer.Core.Observability;
using ReportSyncer.Core.Security;
using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Core.Preflight;

/// <summary>
/// Describes the outcome of preflight checks for a single table.
/// </summary>
/// <param name="JobId">Job identifier.</param>
/// <param name="JobName">Job name.</param>
/// <param name="TargetSchema">Target schema name.</param>
/// <param name="TargetTable">Target table name.</param>
/// <param name="Phase">Sync phase (Preflight).</param>
/// <param name="Estimate">Work estimate captured for this table (may be null on failure).</param>
/// <param name="Permissions">Permissions profile captured for this table (may be null on failure).</param>
/// <param name="Safety">Safety decision (may be null on failure).</param>
/// <param name="Notes">Notes and warnings aggregated during evaluation.</param>
/// <param name="IsAllowed">True when the table passed safety checks.</param>
public sealed record TablePreflightGateReport(
    Guid JobId,
    string JobName,
    string TargetSchema,
    string TargetTable,
    SyncPhase Phase,
    WorkEstimate? Estimate,
    PermissionsProfile? Permissions,
    SafetyDecision? Safety,
    IReadOnlyList<string> Notes,
    bool IsAllowed);
