// ============================================================================
// File: RunConfig.cs
// Author: Gary Wu
// Date: 2025-12-01
// Project: ReportSyncer
// Description: Global runtime configuration settings for synchronization execution.
// ============================================================================

namespace ReportSyncer.Core.Configuration;

/// <summary>
/// Global runtime options for synchronization (dry-run, batch sizes, TVP, ETA smoothing).
/// </summary>
public class RunConfig
{
    /// <summary>
    /// True to run in dry-run mode (no data modifications).
    /// </summary>
    public bool DryRun { get; }

    /// <summary>
    /// Default number of rows to insert per batch.
    /// </summary>
    public int DefaultBatchSize { get; }

    /// <summary>
    /// Number of rows to delete per chunk during deletes.
    /// </summary>
    public int DeleteChunkSize { get; }

    /// <summary>
    /// True to use Table-Valued Parameters for bulk inserts when available.
    /// </summary>
    public bool UseTvpIfAvailable { get; }

    /// <summary>
    /// Optional ETA smoothing factor (0.0 to 1.0). Null disables smoothing.
    /// </summary>
    public double? EtaSmoothing { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="RunConfig"/>.
    /// </summary>
    /// <param name="dryRun">Run in dry-run mode if true.</param>
    /// <param name="defaultBatchSize">Default insert batch size (must be &gt; 0).</param>
    /// <param name="deleteChunkSize">Delete chunk size (must be &gt; 0).</param>
    /// <param name="useTvpIfAvailable">Use TVP when available.</param>
    /// <param name="etaSmoothing">Optional ETA smoothing (0.0–1.0) or null.</param>
    /// <exception cref="System.ArgumentException">Thrown for invalid numeric arguments.</exception>
    public RunConfig(
        bool    dryRun,
        int     defaultBatchSize,
        int     deleteChunkSize,
        bool    useTvpIfAvailable,
        double? etaSmoothing = null)
    {
        if (defaultBatchSize <= 0)
            throw new System.ArgumentException("Default batch size must be positive.", nameof(defaultBatchSize));
        if (deleteChunkSize <= 0)
            throw new System.ArgumentException("Delete chunk size must be positive.", nameof(deleteChunkSize));
        if (etaSmoothing.HasValue && (etaSmoothing.Value < 0.0 || etaSmoothing.Value > 1.0))
            throw new System.ArgumentException("ETA smoothing must be between 0.0 and 1.0.", nameof(etaSmoothing));

        DryRun = dryRun;
        DefaultBatchSize = defaultBatchSize;
        DeleteChunkSize = deleteChunkSize;
        UseTvpIfAvailable = useTvpIfAvailable;
        EtaSmoothing = etaSmoothing;
    }
}