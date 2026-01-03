// ============================================================================
// File: SyncOptionsConfig.cs
// Author: Gary Wu
// Date: 2025-12-01
// Project: ReportSyncer
// Description: Table-level overrides for synchronization execution options.
// ============================================================================

namespace ReportSyncer.Core.Configuration;

/// <summary>
/// <summary>
/// Sync options configuration.
/// </summary>
public class SyncOptionsConfig
{
    /// <summary>
    /// <summary>
    /// Gets the batch size override for this table's insert operations.
    /// When null, the global <see cref="RunConfig.DefaultBatchSize"/> is used.
    /// </summary>
    public int? BatchSize { get; }

    /// <summary>
    /// <summary>
    /// Gets the TVP (Table-Valued Parameter) usage override for this table.
    /// When null, the global <see cref="RunConfig.UseTvpIfAvailable"/> is used.
    /// </summary>
    public bool? UseTvp { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncOptionsConfig"/> class.
    /// </summary>
    /// <param name="batchSize">
    /// Optional batch size override. When null, uses global default. When specified, must be positive.
    /// </param>
    /// <param name="useTvp">
    /// Optional TVP usage override. When null, uses global default.
    /// </param>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="batchSize"/> is specified but not positive.
    /// </exception>
    public SyncOptionsConfig(int? batchSize = null, bool? useTvp = null)
    {
        if (batchSize.HasValue && batchSize.Value <= 0)
            throw new System.ArgumentException("Batch size must be positive when specified.", nameof(batchSize));

        BatchSize = batchSize;
        UseTvp = useTvp;
    }
}