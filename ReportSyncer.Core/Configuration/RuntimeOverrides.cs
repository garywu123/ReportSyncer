// ============================================================================
// File: RuntimeOverrides.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2025-12-05
// Description: Runtime-only override model for run-level settings (not persisted).
// ============================================================================

namespace ReportSyncer.Core.Configuration;

/// <summary>
/// Host-supplied runtime overrides for a single execution of ReportSyncer.
/// </summary>
/// <remarks>
/// <para>
/// This type represents a narrow set of run-level knobs that a host (CLI, Web API)
/// may provide at startup to alter the effective configuration for that run only.
/// Properties are nullable: <c>null</c> means "no override; use YAML value".
/// </para>
/// <para>
/// By design, only non-safety, run-level settings are allowed here. Safety-related
/// flags and schema policies are intentionally not overridable at runtime.
/// </para>
/// </remarks>
public sealed class RuntimeOverrides
{
    /// <summary>
    /// Optional override for dry-run mode. When null, the YAML value is used.
    /// </summary>
    public bool? DryRun { get; init; }

    /// <summary>
    /// Optional override for the default insert batch size. When null, the YAML value is used.
    /// </summary>
    public int? DefaultBatchSize { get; init; }

    /// <summary>
    /// Optional override for delete chunk size. When null, the YAML value is used.
    /// </summary>
    public int? DeleteChunkSize { get; init; }

    /// <summary>
    /// Optional override to prefer Table-Valued Parameters for bulk inserts when available.
    /// When null, the YAML value is used.
    /// </summary>
    public bool? UseTvpIfAvailable { get; init; }

    /// <summary>
    /// Optional ETA smoothing factor override (0.0 to 1.0). Null means use YAML value.
    /// </summary>
    public double? EtaSmoothing { get; init; }

    /// <summary>
    /// Returns <c>true</c> when no overrides are set and the object is effectively empty.
    /// </summary>
    public bool IsEmpty()
    {
        return DryRun is null
         && DefaultBatchSize is null
         && DeleteChunkSize is null
         && UseTvpIfAvailable is null
         && EtaSmoothing is null;
    }
}

/* Policy
 *
 * RuntimeOverrides is a host-provided lightweight DTO intended only for the composition root.
 * Hosts (CLI/WebAPI) should construct a RuntimeOverrides instance and pass it to
 * IConfigurationProvider.LoadAndValidateAsync(path, overrides, ct). The configuration core
 * will validate and merge the overrides via ConfigurationMerger and return a single immutable
 * SyncConfiguration for use by all downstream components.
 *
 * Do NOT pass RuntimeOverrides into business logic, orchestrators, or validators directly.
 */
