// ============================================================================
// File: IColumnMappingRule.cs
// Author: Gary Wu
// Date: 2025-12-23
// Project: ReportSyncer
// Description: Interface for column mapping rules.
// ============================================================================

namespace ReportSyncer.Core.Schema.Mapping;

/// <summary>
/// Rule contract: Inspect the provided <see cref="ColumnMappingContext"/>
/// and either produce a <see cref="ColumnResolution"/> (mapping or error)
/// or return an unhandled resolution to allow the next rule to run.
/// </summary>
internal interface IColumnMappingRule
{
    ColumnResolution TryMap(ColumnMappingContext context);
}
