// ============================================================================
// File: SafetyViolation.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: Represents a safety rule violation detected during validation.
// ============================================================================

namespace ReportSyncer.Core.Security;

/// <summary>
/// Represents a single safety violation produced during evaluation.
/// </summary>
/// <param name="Code">Stable error code for programmatic handling.</param>
/// <param name="Message">Human-readable message describing the violation.</param>
/// <param name="Severity">Severity of the violation.</param>
public sealed record SafetyViolation(
    string Code,
    string Message,
    SafetySeverity Severity);
