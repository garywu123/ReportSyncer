// ============================================================================
// File: SyncPhase.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: Synchronization workflow phases.
// ============================================================================

namespace ReportSyncer.Core.Sync.Contracts;

/// <summary>
/// Represents the current phase of a table synchronization.
/// </summary>
public enum SyncPhase
{
    /// <summary>Preflight checks and validations before execution.</summary>
    Preflight,

    /// <summary>Estimation phase (row counts, sizing, resource estimates).</summary>
    Estimate,

    /// <summary>Phase where target-side deletes are performed.</summary>
    Delete,

    /// <summary>Phase where inserts into the target are performed.</summary>
    Insert
}
