// ============================================================================
// File: SchemaPolicyConfig.cs
// Author: Gary Wu
// Date: 2025-12-01
// Project: ReportSyncer
// Description: Configuration for schema validation policies during pre-flight checks.
// ============================================================================

namespace ReportSyncer.Core.Configuration
{
    /// <summary>
    /// Defines schema validation policies that control how the system responds to schema
    /// differences between source and target tables during pre-flight validation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Schema policies are enforced during the pre-flight validation phase before any data
    /// modification occurs. These policies help catch structural issues early and prevent
    /// potentially incorrect or incomplete data transfers.
    /// </para>
    /// <para>
    /// <b>Mismatch Handling:</b> The <see cref="OnMismatch"/> policy determines whether
    /// schema differences (missing columns, type incompatibilities) cause immediate failure,
    /// generate warnings, or are silently ignored.
    /// </para>
    /// <para>
    /// <b>Primary Key Requirement:</b> When <see cref="RequirePrimaryKey"/> is true, tables
    /// without primary keys are rejected during validation. Primary keys are important for
    /// deduplication and merge logic.
    /// </para>
    /// <para>
    /// <b>Extra Target Columns:</b> When <see cref="AllowExtraTargetColumns"/> is true,
    /// target tables can have columns that don't exist in the source (useful for added
    /// context columns or audit fields).
    /// </para>
    /// </remarks>
    /// <example>
    /// <![CDATA[
    /// // Strict schema policy (safest)
    /// var strictPolicy = new SchemaPolicyConfig(
    ///     onMismatch: SchemaMismatchBehavior.Fail,
    ///     requirePrimaryKey: true,
    ///     allowExtraTargetColumns: false
    /// );
    /// 
    /// // Relaxed policy for reporting syncs (allows extra target columns for context injection)
    /// var reportingPolicy = new SchemaPolicyConfig(
    ///     onMismatch: SchemaMismatchBehavior.Warn,
    ///     requirePrimaryKey: false,
    ///     allowExtraTargetColumns: true
    /// );
    /// ]]>
    /// </example>
    public class SchemaPolicyConfig
    {
        /// <summary>
        /// Gets the behavior when schema mismatches are detected between source and target tables.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Controls the system's response to structural differences:
        /// </para>
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// <see cref="SchemaMismatchBehavior.Fail"/>: Reject the sync immediately.
        /// Safest option, prevents potentially incorrect transfers.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <see cref="SchemaMismatchBehavior.Warn"/>: Log warnings but proceed.
        /// Use when mismatches are expected and acceptable.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <see cref="SchemaMismatchBehavior.Ignore"/>: Silently proceed.
        /// Least safe, only for well-understood scenarios.
        /// </description>
        /// </item>
        /// </list>
        /// </remarks>
        public SchemaMismatchBehavior OnMismatch { get; }

        /// <summary>
        /// Gets a value indicating whether tables must have a primary key defined.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When true, tables without primary keys are rejected during pre-flight validation.
        /// </para>
        /// <para>
        /// Primary keys are important for:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Deduplication logic (WHERE NOT EXISTS patterns)</description></item>
        /// <item><description>Merge/upsert operations</description></item>
        /// <item><description>Identifying unique records for filtering and context injection</description></item>
        /// </list>
        /// <para>
        /// Set to false for heap tables or when using business keys instead of primary keys.
        /// </para>
        /// </remarks>
        public bool RequirePrimaryKey { get; }

        /// <summary>
        /// Gets a value indicating whether target tables are allowed to have columns that don't exist in the source.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When true, target tables can have "extra" columns that aren't present in the source.
        /// These columns are typically:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Context injection columns (e.g., CustomerId in reporting tables)</description></item>
        /// <item><description>Audit columns (CreatedDate, ModifiedDate, SyncTimestamp)</description></item>
        /// <item><description>Computed or derived columns</description></item>
        /// </list>
        /// <para>
        /// When false, target schema must exactly match source schema (strict enforcement).
        /// </para>
        /// <para>
        /// This is commonly set to true for Application → Reporting syncs where context
        /// columns are added, and false for Application → Application or dimension syncs.
        /// </para>
        /// </remarks>
        public bool AllowExtraTargetColumns { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SchemaPolicyConfig"/> class.
        /// </summary>
        /// <param name="onMismatch">
        /// The behavior when schema mismatches are detected.
        /// </param>
        /// <param name="requirePrimaryKey">
        /// Whether to require tables to have primary keys defined.
        /// </param>
        /// <param name="allowExtraTargetColumns">
        /// Whether to allow target tables to have columns not present in the source.
        /// </param>
        public SchemaPolicyConfig(
            SchemaMismatchBehavior onMismatch,
            bool requirePrimaryKey,
            bool allowExtraTargetColumns)
        {
            OnMismatch = onMismatch;
            RequirePrimaryKey = requirePrimaryKey;
            AllowExtraTargetColumns = allowExtraTargetColumns;
        }
    }
}
