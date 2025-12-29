// ============================================================================
// File: PreflightGate.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: Orchestrates work estimation, permission probing, and safety validation.
// ============================================================================

using ReportSyncer.Core.Exceptions;
using ReportSyncer.Core.Observability;
using ReportSyncer.Core.Security;
using ReportSyncer.Core.Sync.Contracts;
using System.Linq;

namespace ReportSyncer.Core.Preflight;

/// <summary>
/// Executes preflight checks (estimate, permissions, safety) for a set of table contexts.
/// </summary>
public sealed class PreflightGate
{
    private readonly WorkEstimator _estimator;
    private readonly IPermissionProfiler _permissionProfiler;
    private readonly ISafetyValidator _safetyValidator;

    public PreflightGate(
        WorkEstimator estimator,
        IPermissionProfiler permissionProfiler,
        ISafetyValidator safetyValidator)
    {
        _estimator = estimator ?? throw new ArgumentNullException(nameof(estimator));
        _permissionProfiler = permissionProfiler ?? throw new ArgumentNullException(nameof(permissionProfiler));
        _safetyValidator = safetyValidator ?? throw new ArgumentNullException(nameof(safetyValidator));
    }

    /// <summary>
    /// Executes the gate for the provided table contexts.
    /// </summary>
    /// <param name="tables">Tables to evaluate.</param>
    /// <param name="confirmation">Optional runtime confirmation for large deletes.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Aggregated gate result.</returns>
    public async Task<PreflightGateResult> EvaluateAsync(
        IReadOnlyList<TableExecutionContext> tables,
        SafetyRuntimeConfirmation? confirmation,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ct.ThrowIfCancellationRequested();

        var reports = new List<TablePreflightGateReport>(tables.Count);
        var blockingCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ctx in tables)
        {
            ct.ThrowIfCancellationRequested();

            WorkEstimate? estimate;
            PermissionsProfile? permissions;
            SafetyDecision? safety;
            var notes = new List<string>();

            try
            {
                estimate = await _estimator.EstimateAsync(ctx, ct).ConfigureAwait(false);
                if (estimate?.Warnings is { Count: > 0 })
                {
                    notes.AddRange(estimate.Warnings);
                }
            }
            catch (WorkEstimationException ex)
            {
                var message = $"Failed to estimate work for job '{ctx.JobName}' table '{ctx.TargetSchema}.{ctx.TargetTable}' during preflight.";
                throw new PreflightGateException(message, ex);
            }

            try
            {
                permissions = await _permissionProfiler.ProbeTablePermissionsAsync(ctx, ct).ConfigureAwait(false);
                if (permissions?.Notes is { Count: > 0 })
                {
                    notes.AddRange(permissions.Notes);
                }
            }
            catch (SyncExecutionException ex)
            {
                var message = $"Failed to probe permissions for job '{ctx.JobName}' table '{ctx.TargetSchema}.{ctx.TargetTable}' during preflight.";
                throw new PreflightGateException(message, ex);
            }

            safety = _safetyValidator.Evaluate(ctx, estimate!, permissions!, confirmation);
            if (safety.Warnings is { Count: > 0 })
            {
                notes.AddRange(safety.Warnings);
            }

            foreach (var violation in safety.Violations.Where(v => v.Severity == SafetySeverity.Blocking))
            {
                blockingCodes.Add(violation.Code);
            }

            var report = new TablePreflightGateReport(
                ctx.JobId,
                ctx.JobName,
                ctx.TargetSchema,
                ctx.TargetTable,
                SyncPhase.Preflight,
                estimate,
                permissions,
                safety,
                notes.AsReadOnly(),
                safety.IsAllowed);

            reports.Add(report);
        }

        var overallAllowed = reports.All(r => r.IsAllowed);
        return new PreflightGateResult(
            overallAllowed,
            reports.AsReadOnly(),
            blockingCodes.ToList().AsReadOnly());
    }
}
