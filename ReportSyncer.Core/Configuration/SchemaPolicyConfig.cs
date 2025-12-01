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
    /// Schema validation policies used during pre-flight checks.
    /// </summary>
    /// <remarks>
    /// Restrictive defaults are recommended; relax settings for reporting syncs as needed.
    /// </remarks>
    public class SchemaPolicyConfig
    {
        /// <summary>
        /// Behavior when schema mismatches are detected.
        /// </summary>
        public SchemaMismatchBehavior OnMismatch { get; }

        /// <summary>
        /// Require a primary key on tables during pre-flight validation.
        /// </summary>
        public bool RequirePrimaryKey { get; }

        /// <summary>
        /// Allow target tables to have extra columns not present in the source.
        /// </summary>
        public bool AllowExtraTargetColumns { get; }

        /// <summary>
        /// Create a new schema policy configuration.
        /// </summary>
        /// <param name="onMismatch">Behavior on schema mismatch.</param>
        /// <param name="requirePrimaryKey">Require primary keys during validation.</param>
        /// <param name="allowExtraTargetColumns">Allow extra columns in target schema.</param>
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
