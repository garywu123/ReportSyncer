// ============================================================================
// File: ColumnMappingConfig.cs
// Author: Gary Wu
// Date: 2025-12-01
// Project: ReportSyncer
// Description: Configuration for column mapping between source and target tables.
// ============================================================================

using System.Collections.Generic;
using JetBrains.Annotations;

namespace ReportSyncer.Core.Configuration
{
    /// <summary>
    /// Configuration for source and target column relationships.
    /// </summary>
    /// <remarks>
    /// </summary>
    /// <example>
    /// </example>
    public class ColumnMappingConfig 
    {
        // Backing fields for optional properties
        /// <summary>
        /// If true, columns with identical names in source and target are auto-mapped.
        /// Defaults to true.
        /// </summary>
        public bool AutomapByName { get; }

        /// <summary>
        /// Explicit source→target column name mappings. Overrides automatic name-based mapping.
        /// Null when none provided.
        /// </summary>
        public IReadOnlyDictionary<string, string>? ExplicitMappings { get; }

        /// <summary>
        /// Additional columns to include in target inserts with constant or parameter values
        /// (supports placeholders like "{CustomerId}"). Null when none provided.
        /// </summary>
        public IReadOnlyList<AddedColumnMappingConfig>? AddedColumns { get; }

        /// <summary>
        /// Create a new column mapping configuration.
        /// </summary>
        /// <param name="automapByName">If true, enable automatic name-based mapping (default true).</param>
        /// <param name="explicitMappings">Optional explicit source→target mappings.</param>
        /// <param name="addedColumns">Optional added columns for target inserts.</param>
        public ColumnMappingConfig(
            bool automapByName = true,
            IDictionary<string, string>? explicitMappings = null,
            IEnumerable<AddedColumnMappingConfig> addedColumns = null)
        {
            AutomapByName = automapByName;
            ExplicitMappings = explicitMappings != null ? new Dictionary<string, string>(explicitMappings) : null;
            AddedColumns = addedColumns?.ToArray();
        }
    }
}
