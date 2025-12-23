// ============================================================================
// File: ColumnResolution.cs
// Author: Gary Wu
// Date: 2025-12-23
// Project: ReportSyncer
// Description: Represents the outcome of a single column mapping rule.
// ============================================================================

namespace ReportSyncer.Core.Schema.Mapping;

/// <summary>
/// The decision returned by a mapping rule.
/// 
/// - If <see cref="Handled"/> is false the chain should continue.
/// - If handled and <see cref="Mapping"/> is non-null a mapping was chosen.
/// - If handled and <see cref="Error"/> is non-null the rule produced an error.
/// </summary>
internal sealed record ColumnResolution(bool Handled, ColumnMapping? Mapping,
    SchemaMappingError? Error)
{
    public static ColumnResolution NotHandled { get; } = new(false, null, null);

    public static ColumnResolution FromMapping(ColumnMapping mapping) => new(true, mapping, null);

    public static ColumnResolution FromError(SchemaMappingError error) => new(true, null, error);
}
