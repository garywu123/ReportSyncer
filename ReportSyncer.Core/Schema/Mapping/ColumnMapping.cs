// ============================================================================
// File: ColumnMapping.cs
// Author: Gary Wu
// Date: 2025-12-20
// Project: ReportSyncer
// Description: Describes how a single target column is mapped from the source snapshot.
// ============================================================================

namespace ReportSyncer.Core.Schema.Mapping;

/// <summary>
/// Mapping information for a single target column.
/// </summary>
/// <param name="SourceColumn">The source column schema, or null for context/constant/ignored mappings.</param>
/// <param name="TargetColumn">The target column schema (never null).</param>
/// <param name="Kind">The mapping kind describing how the target will be populated.</param>
/// <param name="ConstantValue">Optional constant or context value for non-OneToOne mappings.</param>
public sealed record ColumnMapping(
    ColumnSchema? SourceColumn,
    ColumnSchema TargetColumn,
    MappingKind Kind,
    object? ConstantValue
);
