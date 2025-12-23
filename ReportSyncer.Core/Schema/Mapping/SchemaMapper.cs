// ============================================================================
// File: SchemaMapper.cs
// Author: Gary Wu
// Date: 2025-12-22
// Project: ReportSyncer
// Description: Implements the ISchemaMapper contract using the playbook algorithm.
// ============================================================================

using DotNetToolkit.General;
using ReportSyncer.Core.Configuration;


namespace ReportSyncer.Core.Schema.Mapping;

/// <summary>
/// Default implementation of <see cref="ISchemaMapper"/>. Performs table-by-table
/// mapping according to the playbook algorithm and returns a <see cref="SchemaMappingResult"/>.
/// </summary>
public sealed class SchemaMapper : ISchemaMapper
{
    /// <inheritdoc/>
    /// <remarks>
    /// This method assumes the caller (typically an orchestrator) provides snapshots with
    /// appropriate roles (Source/Target). It validates inspection levels but trusts the
    /// caller to provide correctly-roled snapshots.
    /// </remarks>
    public Result<SchemaMappingResult> MapJob(SchemaSnapshot     sourceSnapshot,
                                              SchemaSnapshot     targetSnapshot,
                                              SyncJobConfig      job,
                                              ConnectionConfig   sourceConnection,
                                              ConnectionConfig   targetConnection,
                                              SchemaPolicyConfig schemaPolicy)
    {
        ArgumentNullException.ThrowIfNull(sourceSnapshot);
        ArgumentNullException.ThrowIfNull(targetSnapshot);
        ArgumentNullException.ThrowIfNull(job);

        List<SchemaMappingError>                  errors        = [];
        Dictionary<TableIdentifier, TableMapping> tableMappings = [];

        // Validate inspection level via helper
        if (!ValidateInspectionLevel(targetSnapshot, errors))
        {
            var earlyResult = new SchemaMappingResult(false, errors, tableMappings);
            return Result<SchemaMappingResult>.Fail(
                earlyResult, "Target snapshot requires Full inspection level for FK metadata"
            );
        }

        // Iterate table tasks with guard-style helpers
        foreach (var tableTask in job.Tables)
        {
            if (!TryParseTableIds(tableTask, errors, out var sourceTableId, out var targetTableId))
                continue;

            if (!TryGetTables(
                    sourceSnapshot, targetSnapshot, sourceTableId, targetTableId, errors,
                    out var sourceTable, out var targetTable
                ))
                continue;

            var (columnMappings, columnErrors) = MapColumns(
                tableTask, sourceTable!, targetTable!, sourceTableId, targetTableId,
                schemaPolicy, job.Parameters
            );

            errors.AddRange(columnErrors);

            errors.AddRange(
                ValidatePrimaryKey(targetTable!, sourceTableId, targetTableId, schemaPolicy)
            );

            var tableMapping = new TableMapping(
                sourceTableId, targetTableId, columnMappings, HasWarnings: false
            );

            tableMappings[targetTableId] = tableMapping;
        }

        bool success     = errors.Count == 0;
        var  resultValue = new SchemaMappingResult(success, errors, tableMappings);

        if (success) return Result<SchemaMappingResult>.Ok(resultValue);

        return Result<SchemaMappingResult>.Fail(resultValue, "Schema mapping failed");
    }

    // Helper stubs introduced as part of the refactor (Step 2).
    // Implementations will be filled in subsequent steps.
    private static bool ValidateInspectionLevel(SchemaSnapshot           targetSnapshot,
                                                List<SchemaMappingError> errors)
    {
        ArgumentNullException.ThrowIfNull(targetSnapshot);
        ArgumentNullException.ThrowIfNull(errors);

        if (targetSnapshot.Level != SchemaInspectionLevel.Full)
        {
            errors.Add(
                ColumnMappingHelpers.CreateError(
                    SchemaMappingErrorCode.InsufficientInspectionLevel, null, null, "target", null,
                    null
                )
            );

            return false;
        }

        return true;
    }

    private static bool TryParseTableIds(TableTaskConfig?         tableTask,
                                         List<SchemaMappingError> errors,
                                         out TableIdentifier      sourceTableId,
                                         out TableIdentifier      targetTableId)
    {
        sourceTableId = null!;
        targetTableId = null!;

        if (tableTask == null)
        {
            errors.Add(
                ColumnMappingHelpers.CreateError(
                    SchemaMappingErrorCode.InvalidTableIdentifier, null, null, null, null,
                    null
                )
            );

            return false;
        }

        if (string.IsNullOrWhiteSpace(tableTask.Source)
         || string.IsNullOrWhiteSpace(tableTask.Target))
        {
            errors.Add(
                ColumnMappingHelpers.CreateError(
                    SchemaMappingErrorCode.InvalidTableIdentifier, null, null,
                    tableTask.Source, null, null
                )
            );

            return false;
        }

        try
        {
            sourceTableId = TableIdentifier.Parse(tableTask.Source);
            targetTableId = TableIdentifier.Parse(tableTask.Target);
            return true;
        }
        catch (Exception)
        {
            errors.Add(
                ColumnMappingHelpers.CreateError(
                    SchemaMappingErrorCode.InvalidTableIdentifier, null, null,
                    tableTask.Source, null, null
                )
            );

            return false;
        }
    }

    private static bool TryGetTables(SchemaSnapshot           sourceSnapshot,
                                     SchemaSnapshot           targetSnapshot,
                                     TableIdentifier          sourceTableId,
                                     TableIdentifier          targetTableId,
                                     List<SchemaMappingError> errors,
                                     out TableSchema?         sourceTable,
                                     out TableSchema?         targetTable)
    {
        sourceTable = null;
        targetTable = null;

        if (!sourceSnapshot.TryGetTable(sourceTableId, out sourceTable))
        {
            errors.Add(
                ColumnMappingHelpers.CreateError(
                    SchemaMappingErrorCode.SourceTableNotFound, sourceTableId, targetTableId, null,
                    null, null
                )
            );

            return false;
        }

        if (targetSnapshot.TryGetTable(targetTableId, out targetTable)) return true;
        errors.Add(
            ColumnMappingHelpers.CreateError(
                SchemaMappingErrorCode.TargetTableNotFound, sourceTableId, targetTableId, null,
                null, null
            )
        );

        return false;

    }

    private static (List<ColumnMapping> Mappings, List<SchemaMappingError> Errors) MapColumns(
        TableTaskConfig                      tableTask,
        TableSchema                          sourceTable,
        TableSchema                          targetTable,
        TableIdentifier                      sourceTableId,
        TableIdentifier                      targetTableId,
        SchemaPolicyConfig                   schemaPolicy,
        IReadOnlyDictionary<string, string>? jobParameters)
    {
        var errors         = new List<SchemaMappingError>();
        var columnMappings = new List<ColumnMapping>();

        var effectiveParameters = jobParameters ?? new Dictionary<string, string>();

        var sourceCols = sourceTable.Columns.ToDictionary(
            c => c.Name, c => c, StringComparer.OrdinalIgnoreCase
        );

        var targetCols = targetTable.Columns.ToDictionary(
            c => c.Name, c => c, StringComparer.OrdinalIgnoreCase
        );

        foreach (var targetCol in targetCols.Values)
        {
            var mappingRule = tableTask.ColumnMapping?.Mappings is not null
             && tableTask.ColumnMapping.Mappings.TryGetValue(targetCol.Name, out var mr)
                    ? mr
                    : null;

            var context = new ColumnMappingContext(
                tableTask,
                mappingRule,
                sourceTable,
                targetTable,
                sourceTableId,
                targetTableId,
                schemaPolicy,
                effectiveParameters,
                sourceCols,
                targetCol
            );

            
            var resolution = ResolveColumnMapping(context);

            if (resolution.Error is not null)
            {
                errors.Add(resolution.Error);

                if (resolution.Mapping is null)
                {
                    continue;
                }
            }

            if (resolution.Mapping is null)
            {
                continue;
            }

            foreach (var validator in ColumnMappingValidators)
            {
                var validationError = validator.Validate(resolution.Mapping, context);
                if (validationError is not null)
                {
                    errors.Add(validationError);
                }
            }

            columnMappings.Add(resolution.Mapping);
        }

        return (columnMappings, errors);
    }

    private static List<SchemaMappingError> ValidatePrimaryKey(
        TableSchema targetTable, TableIdentifier sourceTableId, TableIdentifier targetTableId,
        SchemaPolicyConfig schemaPolicy)
    {
        var errs = new List<SchemaMappingError>();
        if (schemaPolicy.RequirePrimaryKey && targetTable.PrimaryKeyColumns.Count == 0)
        {
            errs.Add(
                ColumnMappingHelpers.CreateError(
                    SchemaMappingErrorCode.PrimaryKeyMissing, sourceTableId, targetTableId, null,
                    null, null
                )
            );
        }

        return errs;
    }

    /// <summary>
    /// Helper to create a <see cref="SchemaMappingError"/> with a standardized message template.
    /// </summary>
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
            SchemaMappingErrorCode.TargetColumnTypeIncompatible => $"Incompatible types for column '{column}' in table '{target}': source='{sourceType}', target='{targetType}'.",
            SchemaMappingErrorCode.ContextInjectionParameterMissing => $"Context injection is enabled but job parameter '{column}' is missing.",
            SchemaMappingErrorCode.JobParameterMissingForMapping => $"Job parameter '{column}' required for mapping is missing.",
            SchemaMappingErrorCode.IdentityInsertNotEnabled => $"Target column '{column}' is an IDENTITY column. EnableIdentityInsert must be true to write into it.",
            SchemaMappingErrorCode.ContextInjectionAmbiguous => $"Context injection ambiguity for column '{column}' in table '{target}': source column exists and job parameter is also provided.",
            SchemaMappingErrorCode.ContextColumnMissingInTarget => $"Context injection is enabled but target table '{target}' does not contain column '{column}'.",
            SchemaMappingErrorCode.PrimaryKeyMissing => $"Primary key is required by policy but missing: table '{target}'.",
            SchemaMappingErrorCode.InvalidSnapshotRole => "Schema snapshot role validation failed: source must be Source role and target must be Target role.",
            SchemaMappingErrorCode.InsufficientInspectionLevel => $"Schema mapping requires Full inspection level but {column} snapshot has insufficient detail.",
            _ => "Unknown schema mapping error."
        };

        return new SchemaMappingError(
            code, source, target, column, message,
            sourceType, targetType
        );
    }

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

    private static bool ArrayContains(string[] arr, string v)
    {
        foreach (var x in arr)
            if (string.Equals(x, v, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

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
    /// Check if a column is writable based on its schema properties.
    /// Returns a <see cref="SchemaMappingErrorCode"/> when the column cannot be written to,
    /// or null when the column is writable for the provided mapping kind and table settings.
    /// </summary>
    internal static SchemaMappingErrorCode? GetColumnWritabilityError(ColumnSchema targetCol,
        MappingKind mappingKind,
        bool enableIdentityInsert)
    {
        // Ignored mappings never write, always safe
        if (mappingKind == MappingKind.Ignored) return null;

        // Computed columns cannot be written under any circumstance
        if (targetCol.IsComputed) return SchemaMappingErrorCode.ComputedColumnCannotBeWritten;

        // Rowversion/timestamp columns are auto-generated and cannot be written
        if (targetCol.IsRowVersion) return SchemaMappingErrorCode.RowVersionColumnCannotBeWritten;

        // Identity columns require explicit opt-in via EnableIdentityInsert
        if (targetCol.IsIdentity && !enableIdentityInsert)
            return SchemaMappingErrorCode.IdentityInsertNotEnabled;

        return null; // Column is writable
    }

    /// <summary>
    /// Resolve a single column mapping using the ordered rule chain.
    /// </summary>
    /// <param name="context">Context for the column mapping decision.</param>
    /// <returns>A handled <see cref="ColumnResolution"/> describing the mapping or error.</returns>
    private static ColumnResolution ResolveColumnMapping(ColumnMappingContext context)
    {
        foreach (var rule in ColumnMappingRules)
        {
            var result = rule.TryMap(context);
            if (result.Handled)
            {
                return result;
            }
        }

        return ColumnResolution.NotHandled;
    }

    private static readonly IReadOnlyList<IColumnMappingRule> ColumnMappingRules =
        [
            new ExplicitIgnoreRule(),
            new FromParameterRule(),
            new ConstRule(),
            new FromSourceRule(),
            new ExplicitFallbackIgnoreRule(),
            new AutomapRule(),
            new ExtraTargetAllowedRule(),
            new MissingSourceRule()
        ];

    private static readonly IReadOnlyList<IColumnMappingValidator> ColumnMappingValidators =
        [
            new TypeCompatibilityValidator(),
            new WritabilityValidator()
        ];

    // Column mapping context, resolution and interfaces are defined in
    // separate files under the same namespace to improve discoverability
    // and make unit testing of rules/validators easier.

}
