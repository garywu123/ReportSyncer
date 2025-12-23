// ============================================================================
// File: IColumnMappingValidator.cs
// Author: Gary Wu
// Date: 2025-12-23
// Project: ReportSyncer
// Description: Interface for column mapping validators.
// ============================================================================

namespace ReportSyncer.Core.Schema.Mapping;

/// <summary>
/// Validator contract: After a mapping has been chosen, validators run to
/// detect issues that cannot be decided by rules (type incompatibility,
/// writability constraints, etc.). Validators must not change the mapping;
/// they only return a <see cref="SchemaMappingError"/> when the mapping is
/// invalid for the target column.
/// </summary>
internal interface IColumnMappingValidator
{
    SchemaMappingError? Validate(ColumnMapping mapping, ColumnMappingContext context);
}
