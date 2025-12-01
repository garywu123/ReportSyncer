// ============================================================================
// File: SyncOptionsConfig.cs
// Author: Gary Wu
// Date: 2025-12-01
// Project: ReportSyncer
// Description: Table-level overrides for synchronization execution options.
// ============================================================================

namespace ReportSyncer.Core.Configuration
{
    /// <summary>
    /// Provides table-level overrides for synchronization execution options,
    /// allowing fine-grained control over batch sizes and TVP (Table-Valued Parameter) usage.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These options override the global defaults specified in <see cref="RunConfig"/>.
    /// Use table-level overrides when specific tables require different performance tuning
    /// (e.g., smaller batch sizes for tables with complex triggers, or disabling TVP for
    /// tables with unsupported column types).
    /// </para>
    /// <para>
    /// All properties are nullable. A null value means "use the global default from RunConfig".
    /// </para>
    /// </remarks>
    /// <example>
    /// <![CDATA[
    /// // Override batch size for a specific table
    /// var options = new SyncOptionsConfig(batchSize: 500, useTvp: null);
    /// 
    /// // Disable TVP for a table with incompatible types
    /// var noTvp = new SyncOptionsConfig(batchSize: null, useTvp: false);
    /// ]]>
    /// </example>
    public class SyncOptionsConfig
    {
        /// <summary>
        /// Gets the batch size override for this table's insert operations.
        /// When null, the global <see cref="RunConfig.DefaultBatchSize"/> is used.
        /// </summary>
        /// <remarks>
        /// Batch size controls how many rows are inserted in a single operation.
        /// Smaller batches reduce memory pressure but increase round-trips.
        /// Larger batches improve throughput but may trigger transaction log growth.
        /// </remarks>
        public int? BatchSize { get; }

        /// <summary>
        /// Gets the TVP (Table-Valued Parameter) usage override for this table.
        /// When null, the global <see cref="RunConfig.UseTvpIfAvailable"/> is used.
        /// </summary>
        /// <remarks>
        /// TVP provides better performance for bulk inserts but requires SQL Server 2008+
        /// and may not work with certain column types (e.g., geography, hierarchyid).
        /// Set to false to fall back to individual INSERT statements or bulk copy.
        /// </remarks>
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
}
