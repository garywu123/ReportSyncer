// ============================================================================
// File: SafetyValidator.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: Evaluates safety constraints using work estimates, permissions, and runtime confirmations.
// ============================================================================

using ReportSyncer.Core.Observability;
using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Core.Security;

/// <summary>
/// Implements safety validation rules for table execution contexts.
/// </summary>
public sealed class SafetyValidator : ISafetyValidator
{
    private readonly SafetyValidatorOptions _options;

    public SafetyValidator(SafetyValidatorOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        if (options.LargeDeletePctThreshold < 0 || options.LargeDeletePctThreshold > 100)
            throw new ArgumentOutOfRangeException(nameof(options.LargeDeletePctThreshold), "Threshold must be between 0 and 100.");
        if (options.DeletePctTolerance < 0)
            throw new ArgumentOutOfRangeException(nameof(options.DeletePctTolerance), "Tolerance must be non-negative.");

        _options = options;
    }

    /// <inheritdoc />
    public SafetyDecision Evaluate(
        TableExecutionContext tableCtx,
        WorkEstimate estimate,
        PermissionsProfile permissions,
        SafetyRuntimeConfirmation? confirmation)
    {
        ArgumentNullException.ThrowIfNull(tableCtx);
        ArgumentNullException.ThrowIfNull(estimate);
        ArgumentNullException.ThrowIfNull(permissions);

        var violations = new List<SafetyViolation>();
        var warnings = new List<string>(estimate.Warnings ?? Array.Empty<string>());
        var requiresConfirmation = false;

        // Estimate sanity checks
        if (estimate.EstimatedRowsToDelete < 0 || estimate.EstimatedRowsToInsert < 0)
        {
            AddViolation(violations, SafetyErrorCodes.NegativeEstimate, "Estimated rows cannot be negative.", SafetySeverity.Blocking, warnings);
        }

        if (tableCtx.PreSyncTargetDelete && estimate.DeleteStats is null)
        {
            AddViolation(violations, SafetyErrorCodes.MissingDeleteEstimate, "Delete estimate is missing for a delete-enabled task.", SafetySeverity.Blocking, warnings);
        }

        // Delete scope checks
        if (tableCtx.PreSyncTargetDelete)
        {
            var hasFilters = tableCtx.Filters is { Count: > 0 };
            var hasContext = !string.IsNullOrWhiteSpace(tableCtx.ContextColumnName);
            if (!hasFilters && !hasContext)
            {
                var severity = _options.AllowDeleteWithoutScope ? SafetySeverity.Warning : SafetySeverity.Blocking;
                AddViolation(
                    violations,
                    SafetyErrorCodes.DeleteWithoutScope,
                    "Pre-sync delete is enabled but no filters or context column are provided.",
                    severity,
                    warnings);
            }
        }

        // Large delete confirmation rules
        if (tableCtx.PreSyncTargetDelete && estimate.DeleteStats is not null)
        {
            var deletePct = estimate.DeleteStats.DeletePct ?? estimate.EstimatedDeletePct;
            if (deletePct.HasValue && deletePct.Value >= _options.LargeDeletePctThreshold)
            {
                requiresConfirmation = true;

                if (confirmation?.ConfirmLargeDelete == true)
                {
                    if (confirmation.ConfirmedDeletePct.HasValue)
                    {
                        var delta = Math.Abs(deletePct.Value - confirmation.ConfirmedDeletePct.Value);
                        if (delta > _options.DeletePctTolerance)
                        {
                            AddViolation(
                                violations,
                                SafetyErrorCodes.LargeDeleteConfirmationMismatch,
                                $"Confirmed delete percentage {confirmation.ConfirmedDeletePct.Value:F2}% does not match estimated {deletePct.Value:F2}%.",
                                SafetySeverity.Blocking,
                                warnings);
                        }
                        else
                        {
                            requiresConfirmation = false;
                        }
                    }
                    else
                    {
                        requiresConfirmation = false;
                    }
                }
                else
                {
                    AddViolation(
                        violations,
                        SafetyErrorCodes.LargeDeleteConfirmationRequired,
                        $"Estimated delete percentage {deletePct.Value:F2}% requires confirmation.",
                        SafetySeverity.Blocking,
                        warnings);
                }
            }
        }

        // Permission requirements (RealRun only)
        if (!tableCtx.DryRun)
        {
            if (!permissions.CanReadSource)
            {
                AddViolation(violations, SafetyErrorCodes.NoSourceRead, "Missing permission to read from source.", SafetySeverity.Blocking, warnings);
            }

            if (!permissions.CanReadTarget)
            {
                AddViolation(violations, SafetyErrorCodes.NoTargetRead, "Missing permission to read from target.", SafetySeverity.Blocking, warnings);
            }

            if (permissions.CanInsert is not true)
            {
                AddViolation(violations, SafetyErrorCodes.NoTargetInsert, "Missing permission to insert into target.", SafetySeverity.Blocking, warnings);
            }

            if (tableCtx.PreSyncTargetDelete && permissions.CanDelete is not true)
            {
                AddViolation(violations, SafetyErrorCodes.NoTargetDelete, "Missing permission to delete from target.", SafetySeverity.Blocking, warnings);
            }

            if (tableCtx.EnableIdentityInsert && permissions.CanSetIdentityInsert is not true)
            {
                AddViolation(violations, SafetyErrorCodes.NoIdentityInsert, "Missing permission to set IDENTITY_INSERT on the target.", SafetySeverity.Blocking, warnings);
            }
        }

        var hasBlocking = violations.Any(v => v.Severity == SafetySeverity.Blocking);
        var decision = new SafetyDecision(
            IsAllowed: !hasBlocking && !requiresConfirmation,
            RequiresConfirmation: requiresConfirmation && !hasBlocking,
            Violations: violations.AsReadOnly(),
            Warnings: warnings.AsReadOnly());

        return decision;
    }

    private static void AddViolation(
        List<SafetyViolation> violations,
        string code,
        string message,
        SafetySeverity severity,
        List<string> warnings)
    {
        var violation = new SafetyViolation(code, message, severity);
        violations.Add(violation);
        if (severity == SafetySeverity.Warning)
        {
            warnings.Add(message);
        }
    }
}
