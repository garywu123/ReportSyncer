// ============================================================================
// File: SafetyDecision.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: Aggregated decision returned by safety validation.
// ============================================================================

namespace ReportSyncer.Core.Security;

/// <summary>
/// Represents the outcome of a safety validation check.
/// </summary>
/// <param name="IsAllowed">True when no blocking violations remain and no confirmation is pending.</param>
/// <param name="RequiresConfirmation">True when a confirmation prompt is required before proceeding.</param>
/// <param name="Violations">List of violations (blocking or warnings) discovered.</param>
/// <param name="Warnings">Warnings surfaced during validation (includes estimation warnings).</param>
public sealed record SafetyDecision(
    bool IsAllowed,
    bool RequiresConfirmation,
    IReadOnlyList<SafetyViolation> Violations,
    IReadOnlyList<string> Warnings);
