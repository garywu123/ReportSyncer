// ============================================================================
// File: ColumnMappingConfig.cs
// Author: Gary Wu
// Date: 2025-12-01
// Project: ReportSyncer
// Description: Configuration for column mapping between source and target tables.
// ============================================================================

namespace ReportSyncer.Core.Configuration;

/// <summary>
/// Configuration for source and target column relationships.
/// </summary>
/// <remarks>
/// </remarks>
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
        bool                                    automapByName = true,
        IDictionary<string, ColumnMappingRule>? mappings      = null)
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
    /// <summary>
    /// The name of the source column to map from.
    /// When set, the value for the target column will be taken from the named column in the source row.
    /// This name is matched case-insensitively against source column names when resolving mappings.
    /// </summary>
    public string? FromSource { get; init; }

    /// <summary>
    /// A constant literal value to assign to the target column.
    /// When set, the constant value will be used for every row instead of reading from the source.
    /// Use this for fixed values (for example, a hard-coded flag or default) and prefer string representations
    /// that are compatible with the target column type; validation occurs later in the pipeline.
    /// </summary>
    public string? Const { get; init; }

    /// <summary>
    /// The name of a runtime parameter whose value should be used for the target column.
    /// Parameters are looked up from the synchronization job's parameter collection at execution time.
    /// Use this to inject external or environment-specific values (for example, current user id or run id).
    /// </summary>
    public string? FromParameter { get; init; }

    /// <summary>
    /// When true, the target column will be ignored during synchronization and no value will be written.
    /// This is intended for target-only columns that should not receive data from the source nor be included
    /// in generated INSERT/UPDATE statements.
    /// </summary>
    public bool Ignore { get; init; }
}