// ============================================================================
// File: SchemaMappingError.cs
// Author: Gary Wu
// Date: 2025-12-20
// Project: ReportSyncer
// Description: Structured error details returned by the schema mapper.
// ============================================================================

using ReportSyncer.Core.Schema;

namespace ReportSyncer.Core.Schema.Mapping;

/// <summary>
/// Represents a single mapping error with a machine-friendly code and human message.
/// </summary>
/// <param name="Code">Error code categorizing the failure.</param>
/// <param name="SourceTable">Optional source table identifier related to the error.</param>
/// <param name="TargetTable">Optional target table identifier related to the error.</param>
/// <param name="ColumnName">Optional column name related to the error.</param>
/// <param name="Message">Human-readable message describing the error.</param>
/// <param name="SourceType">Optional source column DbType when relevant.</param>
/// <param name="TargetType">Optional target column DbType when relevant.</param>
public sealed record SchemaMappingError(
    SchemaMappingErrorCode Code,
    TableIdentifier? SourceTable,
    TableIdentifier? TargetTable,
    string? ColumnName,
    string Message,
    string? SourceType,
    string? TargetType
);
