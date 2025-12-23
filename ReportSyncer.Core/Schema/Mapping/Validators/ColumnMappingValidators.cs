// ============================================================================
// File: ColumnMappingValidators.cs
// Author: Gary Wu
// Date: 2025-12-23
// Project: ReportSyncer
// Description: Column mapping validator implementations extracted from SchemaMapper.
// ============================================================================

namespace ReportSyncer.Core.Schema.Mapping;

/// <summary>
/// Validates that a decided mapping is type-compatible. This validator only
/// runs after a mapping has been produced (i.e. it does not participate in
/// decision-making) and returns a <see cref="SchemaMappingError"/> when the
/// source/target types are incompatible.
/// </summary>
internal sealed class TypeCompatibilityValidator : IColumnMappingValidator
{
    public SchemaMappingError? Validate(ColumnMapping mapping, ColumnMappingContext context)
    {
        if (mapping.SourceColumn is null)
        {
            return null;
        }

        if (ColumnMappingHelpers.IsTypeCompatible(mapping.SourceColumn, mapping.TargetColumn))
        {
            return null;
        }

        return ColumnMappingHelpers.CreateError(
            SchemaMappingErrorCode.TargetColumnTypeIncompatible,
            context.SourceTableId,
            context.TargetTableId,
            mapping.TargetColumn.Name,
            mapping.SourceColumn.DbType,
            mapping.TargetColumn.DbType
        );
    }
}

/// <summary>
/// Validates whether the chosen mapping can be physically written to the
/// target column (e.g. not a computed column, not rowversion, identity rules).
/// Returns an error code describing the writability issue when applicable.
/// </summary>
internal sealed class WritabilityValidator : IColumnMappingValidator
{
    public SchemaMappingError? Validate(ColumnMapping mapping, ColumnMappingContext context)
    {
        var writabilityError = ColumnMappingHelpers.GetColumnWritabilityError(
            mapping.TargetColumn,
            mapping.Kind,
            context.TableTask.EnableIdentityInsert
        );

        if (!writabilityError.HasValue)
        {
            return null;
        }

        return ColumnMappingHelpers.CreateError(
            writabilityError.Value,
            context.SourceTableId,
            context.TargetTableId,
            mapping.TargetColumn.Name,
            null,
            null
        );
    }
}
