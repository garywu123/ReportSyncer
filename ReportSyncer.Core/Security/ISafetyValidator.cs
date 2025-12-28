// ============================================================================
// File: ISafetyValidator.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: Contract for validating safety constraints prior to execution.
// ============================================================================

using ReportSyncer.Core.Observability;
using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Core.Security;

/// <summary>
/// Validates safety constraints for a table execution context.
/// </summary>
public interface ISafetyValidator
{
    /// <summary>
    /// Evaluates safety rules for the supplied context and returns a decision.
    /// </summary>
    /// <param name="tableCtx">Execution context for the table.</param>
    /// <param name="estimate">Work estimate (rows to delete/insert, percentages).</param>
    /// <param name="permissions">Permissions profile previously probed.</param>
    /// <param name="confirmation">Optional runtime confirmation inputs (e.g., large delete confirmation).</param>
    /// <returns>A <see cref="SafetyDecision"/> describing whether execution is allowed.</returns>
    SafetyDecision Evaluate(
        TableExecutionContext tableCtx,
        WorkEstimate estimate,
        PermissionsProfile permissions,
        SafetyRuntimeConfirmation? confirmation);
}
