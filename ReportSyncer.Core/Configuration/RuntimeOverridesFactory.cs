// ============================================================================
// File: RuntimeOverridesFactory.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2025-12-06
// Description: Helper factory to build RuntimeOverrides from common host inputs (CLI/WebAPI).
// ============================================================================

namespace ReportSyncer.Core.Configuration;

/// <summary>
/// Factory methods for constructing <see cref="RuntimeOverrides"/> instances from
/// common host input shapes (CLI flags, WebAPI query/body parameters, etc.).
/// </summary>
public static class RuntimeOverridesFactory
{
    /// <summary>
    /// Creates a <see cref="RuntimeOverrides"/> from common CLI options.
    /// Any parameter that is null will not override the YAML value.
    /// </summary>
    /// <param name="dryRunFlag">Nullable dry-run flag from CLI (true/false or null if unspecified).</param>
    /// <param name="defaultBatchSize">Nullable batch size (null if unspecified).</param>
    /// <param name="deleteChunkSize">Nullable delete chunk size (null if unspecified).</param>
    /// <param name="useTvpIfAvailable">Nullable TVP preference (null if unspecified).</param>
    /// <param name="etaSmoothing">Nullable ETA smoothing (null if unspecified).</param>
    /// <returns>A new <see cref="RuntimeOverrides"/> instance.</returns>
    public static RuntimeOverrides FromCliOptions(
        bool?   dryRunFlag        = null,
        int?    defaultBatchSize  = null,
        int?    deleteChunkSize   = null,
        bool?   useTvpIfAvailable = null,
        double? etaSmoothing      = null)
    {
        return new RuntimeOverrides
        {
            DryRun = dryRunFlag,
            DefaultBatchSize = defaultBatchSize,
            DeleteChunkSize = deleteChunkSize,
            UseTvpIfAvailable = useTvpIfAvailable,
            EtaSmoothing = etaSmoothing
        };
    }

    /// <summary>
    /// Creates a <see cref="RuntimeOverrides"/> from Web API inputs. This is identical
    /// to <see cref="FromCliOptions"/> but provided for discoverability in host code.
    /// </summary>
    public static RuntimeOverrides FromWebApiOptions(
        bool?   dryRun          = null,
        int?    batchSize       = null,
        int?    deleteChunkSize = null,
        bool?   useTvp          = null,
        double? etaSmoothing    = null)
        => FromCliOptions(dryRun, batchSize, deleteChunkSize, useTvp, etaSmoothing);
}