// ============================================================================
// File: SchemaMappingErrorCode.cs
// Author: Gary Wu
// Date: 2025-12-20
// Project: ReportSyncer
// Description: Error codes produced by the schema mapper when validating or mapping.
// ============================================================================

namespace ReportSyncer.Core.Schema.Mapping;

/// <summary>
/// Canonical error codes describing why schema mapping failed for a table or column.
/// </summary>
public enum SchemaMappingErrorCode
{
    InvalidTableIdentifier,
    SourceTableNotFound,
    TargetTableNotFound,
    TargetColumnMissingInSource,
    TargetColumnTypeIncompatible,
    /// <summary>
    /// Job parameter referenced by a mapping rule is missing from the job's parameters.
    /// </summary>
    JobParameterMissingForMapping,
    ContextInjectionParameterMissing,
    /// <summary>
    /// Mapping attempted to write to a target IDENTITY column while the table-level
    /// configuration does not allow identity inserts (EnableIdentityInsert == false).
    /// </summary>
    IdentityInsertNotEnabled,
    /// <summary>
    /// Mapping attempted to write to a computed column which is read-only.
    /// </summary>
    ComputedColumnCannotBeWritten,

    /// <summary>
    /// Mapping attempted to write to a rowversion/timestamp column which is auto-generated.
    /// </summary>
    RowVersionColumnCannotBeWritten,
    ContextInjectionAmbiguous,
    ContextColumnMissingInTarget,
    PrimaryKeyMissing,
    InvalidSnapshotRole,
    InsufficientInspectionLevel
}
