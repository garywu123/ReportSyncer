// ============================================================================
// File: TableTaskConfig.cs
// Author: Gary Wu
// Date: 2025-12-01
// Project: ReportSyncer
// Description: Configuration for a single table synchronization task within a job.
// ============================================================================

namespace ReportSyncer.Core.Configuration
{
    /// <summary>
    /// Defines the synchronization configuration for a single table, including source and target
    /// table names, pre-sync delete behavior, filtering, column mapping, and sync options.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each table task within a sync job represents one source→target table pair and all the
    /// rules governing how that data should be synchronized.
    /// </para>
    /// <para>
    /// <b>Pre-Sync Delete Behavior:</b> When <see cref="PreSyncTargetAction"/> is true, the system
    /// deletes matching records from the target before inserting fresh data from the source. This
    /// requires careful configuration:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// If <see cref="Filter"/> is specified, only records matching the filter are deleted.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// If <see cref="Filter"/> is null (full table sync), <see cref="AllowAllDelete"/> must be
    /// explicitly set to true as a safety latch to prevent accidental full-table wipes.
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// <b>Identity Insert:</b> When <see cref="EnableIdentityInsert"/> is true, identity columns
    /// are explicitly inserted from source values rather than auto-generated on the target.
    /// </para>
    /// </remarks>
    public class TableTaskConfig
    {
        /// <summary>
        /// Gets the source table name (can include schema, e.g., "dbo.Customers").
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// Gets the target table name (can include schema, e.g., "dbo.Customers").
        /// </summary>
        public string Target { get; }

        /// <summary>
        /// Gets a value indicating whether this table task is enabled.
        /// When false, the table is skipped during synchronization.
        /// </summary>
        /// <remarks>
        /// Use this to temporarily disable specific tables without removing them from
        /// the configuration, useful for testing or phased rollouts.
        /// </remarks>
        public bool Enabled { get; }

        /// <summary>
        /// Gets a value indicating whether to delete matching records from the target
        /// before inserting data from the source.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When true, performs a "delete + insert" pattern (similar to MERGE/upsert but simpler).
        /// The scope of deletion depends on <see cref="Filter"/>:
        /// </para>
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// If <see cref="Filter"/> is specified: Deletes only records matching the filter conditions.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// If <see cref="Filter"/> is null: Deletes ALL records (requires <see cref="AllowAllDelete"/> = true).
        /// </description>
        /// </item>
        /// </list>
        /// </remarks>
        public bool PreSyncTargetAction { get; }

        /// <summary>
        /// Gets a value indicating whether unscoped deletes (deleting all target records) are allowed.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>SAFETY CRITICAL:</b> This property defaults to false and acts as a safety latch.
        /// </para>
        /// <para>
        /// When <see cref="PreSyncTargetAction"/> is true and <see cref="Filter"/> is null,
        /// the system will delete ALL records from the target table. To prevent accidental
        /// full-table wipes, you must explicitly set this to true.
        /// </para>
        /// <para>
        /// If <see cref="Filter"/> is specified, this property is not required (filtered deletes
        /// are considered safe).
        /// </para>
        /// </remarks>
        public bool AllowAllDelete { get; }

        /// <summary>
        /// Gets a value indicating whether to enable IDENTITY_INSERT for this table's target inserts.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When true, identity column values are explicitly inserted from the source rather than
        /// being auto-generated on the target. This preserves identity values across environments.
        /// </para>
        /// <para>
        /// Requires appropriate database permissions (ALTER permission on the table).
        /// </para>
        /// </remarks>
        public bool EnableIdentityInsert { get; }

        /// <summary>
        /// Gets the filter configuration for this table, or null for full table synchronization.
        /// </summary>
        /// <remarks>
        /// Filters reduce the scope of data synchronized, enabling incremental syncs and
        /// reducing resource consumption. When null, all records from the source are synchronized.
        /// </remarks>
        public FilterConfig? Filter { get; }

        /// <summary>
        /// Gets the column mapping configuration, or null to use default automatic mapping.
        /// </summary>
        /// <remarks>
        /// When null, the system automatically maps columns by name between source and target.
        /// Specify a <see cref="ColumnMappingConfig"/> to control explicit mappings, disable
        /// auto-mapping, or add context injection columns.
        /// </remarks>
        public ColumnMappingConfig? ColumnMapping { get; }

        /// <summary>
        /// Gets the business key configuration for deduplication, or null if not using key-based deduplication.
        /// </summary>
        /// <remarks>
        /// Business keys are used for merge/upsert patterns with WHERE NOT EXISTS logic.
        /// When null, the system relies on pre-sync deletes or assumes all records are new.
        /// </remarks>
        public KeyConfig? Keys { get; }

        /// <summary>
        /// Gets the table-level sync options that override global defaults, or null to use global defaults.
        /// </summary>
        /// <remarks>
        /// Use this to override batch sizes or TVP usage for specific tables that need
        /// different performance tuning than the global <see cref="RunConfig"/> settings.
        /// </remarks>
        public SyncOptionsConfig? SyncOptions { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TableTaskConfig"/> class.
        /// </summary>
        /// <param name="source">
        /// The source table name. Must not be null or empty.
        /// </param>
        /// <param name="target">
        /// The target table name. Must not be null or empty.
        /// </param>
        /// <param name="enabled">
        /// Whether this table task is enabled. Defaults to true.
        /// </param>
        /// <param name="preSyncTargetAction">
        /// Whether to delete matching target records before inserting. Defaults to false.
        /// </param>
        /// <param name="allowAllDelete">
        /// Whether to allow unscoped deletes (all records). Defaults to false for safety.
        /// Required to be true if preSyncTargetAction is true and filter is null.
        /// </param>
        /// <param name="enableIdentityInsert">
        /// Whether to enable IDENTITY_INSERT for this table. Defaults to false.
        /// </param>
        /// <param name="filter">
        /// Optional filter configuration for scoped synchronization.
        /// </param>
        /// <param name="columnMapping">
        /// Optional column mapping configuration. When null, automatic mapping by name is used.
        /// </param>
        /// <param name="keys">
        /// Optional business key configuration for deduplication.
        /// </param>
        /// <param name="syncOptions">
        /// Optional table-level sync options that override global defaults.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="source"/> or <paramref name="target"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="source"/> or <paramref name="target"/> is empty or whitespace.
        /// </exception>
        public TableTaskConfig(
            string source,
            string target,
            bool enabled = true,
            bool preSyncTargetAction = false,
            bool allowAllDelete = false,
            bool enableIdentityInsert = false,
            FilterConfig? filter = null,
            ColumnMappingConfig? columnMapping = null,
            KeyConfig? keys = null,
            SyncOptionsConfig? syncOptions = null)
        {
            ArgumentNullException.ThrowIfNull(source);
            if (string.IsNullOrWhiteSpace(source))
                throw new ArgumentException("Source table name cannot be empty or whitespace.", nameof(source));
            ArgumentNullException.ThrowIfNull(target);
            if (string.IsNullOrWhiteSpace(target))
                throw new ArgumentException("Target table name cannot be empty or whitespace.", nameof(target));

            Source = source;
            Target = target;
            Enabled = enabled;
            PreSyncTargetAction = preSyncTargetAction;
            AllowAllDelete = allowAllDelete;
            EnableIdentityInsert = enableIdentityInsert;
            Filter = filter;
            ColumnMapping = columnMapping;
            Keys = keys;
            SyncOptions = syncOptions;
        }
    }
}
