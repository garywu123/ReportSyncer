// ============================================================================
// File: ColumnMappingContext.cs
// Author: Gary Wu
// Date: 2025-12-23
// Project: ReportSyncer
// Description: Shared context passed to column mapping rules and validators.
// ============================================================================

namespace ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Configuration;

/// <summary>
/// Shared context passed into column mapping rules and validators.
/// 
/// Contains table/column metadata, job parameters, policy flags and the
/// explicit per-column rule (if any). Rules use this context to make
/// mapping decisions; validators use it to provide richer error messages.
/// </summary>
internal sealed record ColumnMappingContext(
    TableTaskConfig TableTask,
    ColumnMappingRule? ExplicitRule,
    TableSchema SourceTable,
    TableSchema TargetTable,
    TableIdentifier SourceTableId,
    TableIdentifier TargetTableId,
    SchemaPolicyConfig SchemaPolicy,
    IReadOnlyDictionary<string, string> JobParameters,
    IReadOnlyDictionary<string, ColumnSchema> SourceColumns,
    ColumnSchema TargetColumn)
{
    /// <summary>
    /// Indicates whether automatic name-based mapping is enabled for this table.
    /// </summary>
    public bool AutomapEnabled => TableTask.ColumnMapping?.AutomapByName != false;

    /// <summary>
    /// Attempts to resolve a source column by name using case-insensitive matching.
    /// </summary>
    public bool TryGetSourceColumn(string columnName, out ColumnSchema? column)
    {
        var found = SourceColumns.TryGetValue(columnName, out var tmp);
        column = tmp;
        return found;
    }
}
