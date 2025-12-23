// ============================================================================
// File: ColumnMappingHelpers.cs
// Author: Gary Wu
// Date: 2025-12-23
// Project: ReportSyncer
// Description: Shared helpers extracted for column mapping rules and validators.
// ============================================================================

namespace ReportSyncer.Core.Schema.Mapping;

/// <summary>
/// Lightweight helpers used by column mapping rules and validators.
/// 
/// Notes:
/// - These helpers centralize error construction and simple type/parameter helpers
///   so rules and validators can remain small and easily unit-tested.
/// - Keep implementations deterministic and side-effect free to simplify testing.
/// </summary>
internal static class ColumnMappingHelpers
{
    /// <summary>
    /// Create a <see cref="SchemaMappingError"/> with a standard message for the
    /// provided <see cref="SchemaMappingErrorCode"/> and context values.
    /// </summary>
    /// <param name="code">Error code.</param>
    /// <param name="source">Source table identifier when applicable.</param>
    /// <param name="target">Target table identifier when applicable.</param>
    /// <param name="column">Column name related to the error when applicable.</param>
    /// <param name="sourceType">Source column DB type if relevant.</param>
    /// <param name="targetType">Target column DB type if relevant.</param>
    /// <returns>A populated <see cref="SchemaMappingError"/> instance.</returns>
    internal static SchemaMappingError CreateError(SchemaMappingErrorCode code,
        TableIdentifier? source, TableIdentifier? target,
        string? column, string? sourceType,
        string? targetType)
    {
        string message = code switch
        {
            SchemaMappingErrorCode.InvalidTableIdentifier => $"Invalid table identifier format: '{column}'.",
            SchemaMappingErrorCode.SourceTableNotFound => $"Source table not found in snapshot: '{source}'.",
            SchemaMappingErrorCode.TargetTableNotFound => $"Target table not found in snapshot: '{target}'.",
            SchemaMappingErrorCode.TargetColumnMissingInSource => $"Target column '{column}' in table '{target}' has no matching source column.",
            SchemaMappingErrorCode.TargetColumnTypeIncompatible =>
                $"Incompatible types for column '{column}' in table '{target}': source='{sourceType}', target='{targetType}'.",
            SchemaMappingErrorCode.ContextInjectionParameterMissing =>
                $"Context injection is enabled but job parameter '{column}' is missing.",
            SchemaMappingErrorCode.JobParameterMissingForMapping => $"Job parameter '{column}' required for mapping is missing.",
            SchemaMappingErrorCode.IdentityInsertNotEnabled => $"Target column '{column}' is an IDENTITY column. EnableIdentityInsert must be true to write into it.",
            SchemaMappingErrorCode.ContextInjectionAmbiguous =>
                $"Context injection ambiguity for column '{column}' in table '{target}': source column exists and job parameter is also provided.",
            SchemaMappingErrorCode.ContextColumnMissingInTarget =>
                $"Context injection is enabled but target table '{target}' does not contain column '{column}'.",
            SchemaMappingErrorCode.PrimaryKeyMissing => $"Primary key is required by policy but missing: table '{target}'.",
            SchemaMappingErrorCode.InvalidSnapshotRole =>
                "Schema snapshot role validation failed: source must be Source role and target must be Target role.",
            SchemaMappingErrorCode.InsufficientInspectionLevel =>
                $"Schema mapping requires Full inspection level but {column} snapshot has insufficient detail.",
            _ => "Unknown schema mapping error."
        };

        return new SchemaMappingError(
            code, source, target, column, message,
            sourceType, targetType
        );
    }

    /// <summary>
    /// Determine whether the source and target column types are compatible for
    /// a straightforward one-to-one copy. This uses family-based heuristics
    /// (string/int/decimal/date/guid/bool) rather than precise length/scale checks.
    /// </summary>
    internal static bool IsTypeCompatible(ColumnSchema sourceCol, ColumnSchema targetCol)
    {
        var s = (sourceCol.DbType ?? string.Empty).ToLowerInvariant();
        var t = (targetCol.DbType ?? string.Empty).ToLowerInvariant();
        if (s == t) return true;

        var stringFamily = new[] { "varchar", "nvarchar", "char", "nchar", "text", "ntext" };
        var intFamily = new[] { "tinyint", "smallint", "int", "bigint" };
        var decimalFamily = new[] { "decimal", "numeric", "money", "smallmoney" };
        var dateFamily = new[] { "date", "datetime", "datetime2", "smalldatetime", "time" };
        var guidFamily = new[] { "uniqueidentifier" };
        var boolFamily = new[] { "bit" };

        if ((ArrayContains(stringFamily, s) && ArrayContains(stringFamily, t)) ||
            (ArrayContains(intFamily, s) && ArrayContains(intFamily, t)) ||
            (ArrayContains(decimalFamily, s) && ArrayContains(decimalFamily, t)) ||
            (ArrayContains(dateFamily, s) && ArrayContains(dateFamily, t)) ||
            (ArrayContains(guidFamily, s) && ArrayContains(guidFamily, t)) ||
            (ArrayContains(boolFamily, s) && ArrayContains(boolFamily, t)))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Case-insensitive lookup for a job parameter.
    /// </summary>
    /// <returns>True and the value when found; otherwise false.</returns>
    internal static bool TryGetJobParameter(IReadOnlyDictionary<string, string> parameters, string key, out string? value)
    {
        if (parameters == null)
        {
            value = null;
            return false;
        }

        foreach (var kvp in parameters)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(kvp.Key, key))
            {
                value = kvp.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Returns a <see cref="SchemaMappingErrorCode"/> describing why a target
    /// column cannot be written for the provided <paramref name="mappingKind"/>,
    /// or null when the column is writable.
    /// </summary>
    internal static SchemaMappingErrorCode? GetColumnWritabilityError(ColumnSchema targetCol,
        MappingKind mappingKind,
        bool enableIdentityInsert)
    {
        if (mappingKind == MappingKind.Ignored) return null;

        if (targetCol.IsComputed) return SchemaMappingErrorCode.ComputedColumnCannotBeWritten;

        if (targetCol.IsRowVersion) return SchemaMappingErrorCode.RowVersionColumnCannotBeWritten;

        if (targetCol.IsIdentity && !enableIdentityInsert)
            return SchemaMappingErrorCode.IdentityInsertNotEnabled;

        return null;
    }

    /// <summary>
    /// Case-insensitive containment helper used by the family checks.
    /// </summary>
    private static bool ArrayContains(string[] arr, string v)
    {
        foreach (var x in arr)
            if (string.Equals(x, v, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
