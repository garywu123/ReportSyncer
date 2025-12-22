// ============================================================================
// File: ColumnMappingConfig.cs
// Author: Gary Wu
// Date: 2025-12-01
// Project: ReportSyncer
// Description: Configuration for column mapping between source and target tables.
// ============================================================================

using System;
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
        /// Unified mapping rules for target columns. Key is the target column name (case-insensitive).
        /// Each entry describes the source or constant/parameter that should populate the target column.
        /// Null when none provided.
        /// </summary>
        public IReadOnlyDictionary<string, ColumnMappingRule>? Mappings { get; }

        /// <summary>
        /// Create a new column mapping configuration.
        /// </summary>
        /// <param name="automapByName">If true, enable automatic name-based mapping (default true).</param>
        /// <param name="mappings">Optional unified column mapping rules, keyed by target column name.</param>
        public ColumnMappingConfig(
            bool automapByName = true,
            IDictionary<string, ColumnMappingRule>? mappings = null)
        {
            AutomapByName = automapByName;
            Mappings = mappings != null ? new Dictionary<string, ColumnMappingRule>(mappings, StringComparer.OrdinalIgnoreCase) : null;
        }
    }

    /// <summary>
    /// A rule describing how to populate a specific target column.
    /// Only one of FromSource, Const, FromParameter or Ignore should be set.
    /// </summary>
    public sealed class ColumnMappingRule
    {
        public string? FromSource { get; init; }
        public string? Const { get; init; }
        public string? FromParameter { get; init; }
        public bool Ignore { get; init; }
    }
}
