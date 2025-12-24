// ============================================================================
// File: SchemaMappingResult.cs
// Author: Gary Wu
// Date: 2025-12-20
// Project: ReportSyncer
// Description: Top-level result returned by the schema mapper for a job.
// ============================================================================

namespace ReportSyncer.Core.Schema.Mapping;

/// <summary>
/// Overall mapping result for a sync job: success flag, collected errors, and table mappings.
/// </summary>
/// <param name="Success">True when mapping completed with no errors.</param>
/// <param name="Errors">List of mapping errors encountered.</param>
/// <param name="TableMappings">Dictionary of produced table mappings keyed by target table id.</param>
public sealed record SchemaMappingResult(
    bool Success,
    IReadOnlyList<SchemaMappingError> Errors,
    IReadOnlyDictionary<TableIdentifier, TableMapping> TableMappings
);
