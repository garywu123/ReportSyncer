// ============================================================================
// File: SafetyRuntimeConfirmation.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: Runtime confirmation DTO for large delete safeguards.
// ============================================================================

namespace ReportSyncer.Core.Security;

/// <summary>
/// Represents user-provided confirmation for potentially destructive operations.
/// </summary>
/// <param name="ConfirmLargeDelete">True when the user has explicitly confirmed a large delete.</param>
/// <param name="ConfirmedDeletePct">Optional percentage the user acknowledged; used to detect mismatches.</param>
public sealed record SafetyRuntimeConfirmation(
    bool ConfirmLargeDelete,
    double? ConfirmedDeletePct);
