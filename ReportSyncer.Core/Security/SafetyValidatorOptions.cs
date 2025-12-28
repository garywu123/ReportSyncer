// ============================================================================
// File: SafetyValidatorOptions.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: Options controlling safety validation thresholds and toggles.
// ============================================================================

namespace ReportSyncer.Core.Security;

/// <summary>
/// Options controlling safety validation behavior.
/// </summary>
/// <param name="LargeDeletePctThreshold">
/// Percentage (0-100) that triggers large-delete confirmation when exceeded.
/// </param>
/// <param name="DeletePctTolerance">
/// Allowed difference between confirmed delete percentage and the estimated percentage.
/// </param>
/// <param name="AllowDeleteWithoutScope">
/// When true, delete-without-scope is downgraded to a warning instead of blocking.
/// </param>
public sealed record SafetyValidatorOptions(
    double LargeDeletePctThreshold,
    double DeletePctTolerance,
    bool AllowDeleteWithoutScope);
