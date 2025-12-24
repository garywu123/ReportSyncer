// ============================================================================
// File: TableMapping.cs
// Author: Gary Wu
// Date: 2025-12-20
// Project: ReportSyncer
// Description: Aggregated mapping for a source→target table pair.
// ============================================================================

namespace ReportSyncer.Core.Schema.Mapping;

/// <summary>
/// Represents the mapping result for a single table pair, including per-column mappings.
/// </summary>
/// <param name="SourceTable">Source table identifier.</param>
/// <param name="TargetTable">Target table identifier.</param>
/// <param name="ColumnMappings">List of column mappings for the target table.</param>
/// <param name="HasWarnings">True when the mapping completed but produced non-fatal warnings.</param>
public sealed record TableMapping(
    TableIdentifier SourceTable,
    TableIdentifier TargetTable,
    IReadOnlyList<ColumnMapping> ColumnMappings,
    bool HasWarnings
);
