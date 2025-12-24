// ============================================================================
// File: RunConfig.cs
// Author: Gary Wu
// Date: 2025-12-01
// Project: ReportSyncer
// Description: Global runtime configuration settings for synchronization execution.
// ============================================================================

namespace ReportSyncer.Core.Configuration
{
    /// <summary>
    /// Defines global runtime options that control synchronization execution behavior,
    /// including dry-run mode, batch sizes, and TVP usage.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These settings apply to all sync jobs unless overridden at the table level via
    /// <see cref="SyncOptionsConfig"/>.
    /// </para>
    /// <para>
    /// <b>Dry-Run Mode:</b> When <see cref="DryRun"/> is true, the system performs all
    /// validation, pre-flight checks, and work estimation but does not execute any
    /// data modification operations (no DELETE or INSERT). Use this to validate
    /// configurations and estimate work without affecting databases.
    /// </para>
    /// <para>
    /// <b>Batch Size:</b> Controls how many rows are inserted in a single batch operation.
    /// Smaller batches reduce memory and transaction log pressure but increase round-trips.
    /// </para>
    /// <para>
    /// <b>Delete Chunk Size:</b> Controls how many rows are deleted per chunk during
    /// pre-sync delete operations. Prevents transaction log bloat for large deletes.
    /// </para>
    /// <para>
    /// <b>TVP (Table-Valued Parameters):</b> Enables high-performance bulk inserts using
    /// SQL Server TVPs. May not work with all column types (e.g., geography, hierarchyid).
    /// </para>
    /// </remarks>
    /// <example>
    /// <![CDATA[
    /// // Standard configuration for production sync
    /// var runConfig = new RunConfig(
    ///     dryRun: false,
    ///     defaultBatchSize: 2000,
    ///     deleteChunkSize: 5000,
    ///     useTvpIfAvailable: true,
    ///     etaSmoothing: 0.3
    /// );
    /// 
    /// // Dry-run configuration for validation
    /// var dryRunConfig = new RunConfig(
    ///     dryRun: true,
    ///     defaultBatchSize: 2000,
    ///     deleteChunkSize: 5000,
    ///     useTvpIfAvailable: true
    /// );
    /// ]]>
    /// </example>
    public class RunConfig
    {
        /// <summary>
        /// Gets a value indicating whether to run in dry-run mode (no data modifications).
        /// </summary>
        /// <remarks>
        /// <para>
        /// When true:
        /// </para>
        /// <list type="bullet">
        /// <item><description>All validation and pre-flight checks are performed</description></item>
        /// <item><description>Work estimation (row counts, delete estimates) is calculated</description></item>
        /// <item><description>No DELETE or INSERT operations are executed</description></item>
        /// <item><description>User is shown what would happen without actual changes</description></item>
        /// </list>
        /// <para>
        /// Dry-run mode is safe to run against production databases for validation purposes.
        /// </para>
        /// </remarks>
        public bool DryRun { get; }

        /// <summary>
        /// Gets the default number of rows to insert per batch operation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Batch size affects performance and resource usage:
        /// </para>
        /// <list type="bullet">
        /// <item><description><b>Small batches (500-1000):</b> Lower memory, smaller transactions, more round-trips</description></item>
        /// <item><description><b>Medium batches (2000-5000):</b> Balanced performance for most scenarios</description></item>
        /// <item><description><b>Large batches (10000+):</b> Maximum throughput but higher memory and transaction log usage</description></item>
        /// </list>
        /// <para>
        /// Can be overridden per table via <see cref="SyncOptionsConfig.BatchSize"/>.
        /// </para>
        /// </remarks>
        public int DefaultBatchSize { get; }

        /// <summary>
        /// Gets the number of rows to delete per chunk during pre-sync delete operations.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Chunked deletes prevent transaction log bloat when removing large numbers of rows.
        /// Each chunk is committed separately, allowing transaction log truncation between chunks.
        /// </para>
        /// <para>
        /// Typical values: 5000-10000 rows per chunk.
        /// </para>
        /// </remarks>
        public int DeleteChunkSize { get; }

        /// <summary>
        /// Gets a value indicating whether to use Table-Valued Parameters (TVP) for bulk inserts when available.
        /// </summary>
        /// <remarks>
        /// <para>
        /// TVP provides better performance than individual INSERT statements but has limitations:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Requires SQL Server 2008 or later</description></item>
        /// <item><description>May not work with certain column types (geography, hierarchyid, etc.)</description></item>
        /// <item><description>Requires appropriate permissions</description></item>
        /// </list>
        /// <para>
        /// When false or when TVP is not compatible, the system falls back to bulk copy or batched INSERTs.
        /// Can be overridden per table via <see cref="SyncOptionsConfig.UseTvp"/>.
        /// </para>
        /// </remarks>
        public bool UseTvpIfAvailable { get; }

        /// <summary>
        /// Gets the optional ETA (Estimated Time to Arrival) smoothing factor for progress reporting.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When specified, uses exponential smoothing to reduce ETA jitter in progress displays.
        /// Valid range: 0.0 (no smoothing, pure current rate) to 1.0 (maximum smoothing, mostly historical).
        /// </para>
        /// <para>
        /// Typical values: 0.2-0.4 for balanced responsiveness and stability.
        /// </para>
        /// <para>
        /// When null, ETA calculation uses a simple moving average or unsmoothed calculation.
        /// </para>
        /// </remarks>
        public double? EtaSmoothing { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RunConfig"/> class.
        /// </summary>
        /// <param name="dryRun">
        /// Whether to run in dry-run mode (no data modifications).
        /// </param>
        /// <param name="defaultBatchSize">
        /// The default batch size for insert operations. Must be positive.
        /// </param>
        /// <param name="deleteChunkSize">
        /// The chunk size for delete operations. Must be positive.
        /// </param>
        /// <param name="useTvpIfAvailable">
        /// Whether to use Table-Valued Parameters for bulk inserts when available.
        /// </param>
        /// <param name="etaSmoothing">
        /// Optional ETA smoothing factor (0.0 to 1.0). Null for no smoothing.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="defaultBatchSize"/> or <paramref name="deleteChunkSize"/>
        /// is not positive, or when <paramref name="etaSmoothing"/> is outside the valid range.
        /// </exception>
        public RunConfig(
            bool dryRun,
            int defaultBatchSize,
            int deleteChunkSize,
            bool useTvpIfAvailable,
            double? etaSmoothing = null)
        {
            if (defaultBatchSize <= 0)
                throw new ArgumentException("Default batch size must be positive.", nameof(defaultBatchSize));
            if (deleteChunkSize <= 0)
                throw new ArgumentException("Delete chunk size must be positive.", nameof(deleteChunkSize));
            if (etaSmoothing.HasValue && (etaSmoothing.Value < 0.0 || etaSmoothing.Value > 1.0))
                throw new ArgumentException("ETA smoothing must be between 0.0 and 1.0.", nameof(etaSmoothing));

            DryRun = dryRun;
            DefaultBatchSize = defaultBatchSize;
            DeleteChunkSize = deleteChunkSize;
            UseTvpIfAvailable = useTvpIfAvailable;
            EtaSmoothing = etaSmoothing;
        }
    }
}
