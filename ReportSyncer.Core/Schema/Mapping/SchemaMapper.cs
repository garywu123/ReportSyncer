// ============================================================================
// File: SchemaMapper.cs
// Author: Gary Wu
// Date: 2025-12-20
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
    public Result<SchemaMappingResult> MapJob(
        SchemaSnapshot sourceSnapshot,
        SchemaSnapshot targetSnapshot,
        SyncJobConfig job,
        ConnectionConfig sourceConnection,
        ConnectionConfig targetConnection,
        SchemaPolicyConfig schemaPolicy)
    {
        if (sourceSnapshot == null) throw new ArgumentNullException(nameof(sourceSnapshot));
        if (targetSnapshot == null) throw new ArgumentNullException(nameof(targetSnapshot));
        if (job == null) throw new ArgumentNullException(nameof(job));

        List<SchemaMappingError> errors = new();
        Dictionary<TableIdentifier, TableMapping> tableMappings = new();

        // Validate inspection levels: Target requires Full for FK metadata (dependency ordering).
        // Source can be ExistenceOnly or Full (both now provide column metadata for type checks).
        // Note: As of the updated design, ExistenceOnly loads columns but skips FK loading.
        if (targetSnapshot.Level != SchemaInspectionLevel.Full)
        {
            errors.Add(CreateError(SchemaMappingErrorCode.InsufficientInspectionLevel, null, null, "target", null, null));
        }

        // If target inspection level is insufficient, return early
        if (errors.Count > 0)
        {
            var earlyResult = new SchemaMappingResult(false, errors, tableMappings);
            return Result<SchemaMappingResult>.Fail(earlyResult, "Target snapshot requires Full inspection level for FK metadata");
        }

        // 针对每个 table task，执行源表和目标表的映射。
        foreach (var tableTask in job.Tables)
        {
            TableIdentifier sourceTableId;
            TableIdentifier targetTableId;

            // Defensive: ensure source/target identifiers are provided before parsing.
            if (string.IsNullOrWhiteSpace(tableTask?.Source) || string.IsNullOrWhiteSpace(tableTask?.Target))
            {
                errors.Add(CreateError(SchemaMappingErrorCode.InvalidTableIdentifier, null, null, tableTask?.Source ?? tableTask?.Target, null, null));
                continue;
            }

            try
            {
                // 确保 string 能成功解析为 TableIdentifier。
                sourceTableId = TableIdentifier.Parse(tableTask.Source!);
                targetTableId = TableIdentifier.Parse(tableTask.Target!);
            }
            catch (Exception)
            {
                // If parsing fails add InvalidTableIdentifier error and continue mapping other tables.
                errors.Add(CreateError(SchemaMappingErrorCode.InvalidTableIdentifier, null, null, tableTask.Source ?? tableTask.Target, null, null));
                continue;
            }

            if (!sourceSnapshot.TryGetTable(sourceTableId, out var sourceTable))
            {
                // Defensive check: if orchestrator correctly inspected tables per job.Tables,
                // this should not happen. But if snapshot is incomplete, we catch it here.
                errors.Add(CreateError(SchemaMappingErrorCode.SourceTableNotFound, sourceTableId, targetTableId, null, null, null));
                continue;
            }

            if (!targetSnapshot.TryGetTable(targetTableId, out var targetTable))
            {
                errors.Add(CreateError(SchemaMappingErrorCode.TargetTableNotFound, sourceTableId, targetTableId, null, null, null));
                continue;
            }

            var sourceCols = sourceTable!.Columns.ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);
            var targetCols = targetTable!.Columns.ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);

            // No automatic context injection. Treat all target columns uniformly and require
            // explicit mapping or automap to succeed.

            List<ColumnMapping> columnMappings = new();

            // 针对目标表的每一列，尝试在源表中找到匹配列，并根据情况创建相应的 ColumnMapping 或记录错误。
            foreach (var targetCol in targetTable.Columns)
            {
                // 根据 target 列名查找是否存在用户定义的映射规则。
                var mappingRule = tableTask.ColumnMapping?.Mappings is not null && tableTask.ColumnMapping.Mappings.TryGetValue(targetCol.Name, out var mr)
                    ? mr
                    : null;

                if (mappingRule is not null)
                {
                    if (mappingRule.Ignore)
                    {
                        columnMappings.Add(new ColumnMapping(null, targetCol, MappingKind.Ignored, null));
                        continue;
                    }

                    // 如果映射规则指定了 FromParameter，则尝试从作业参数中获取对应值。
                    if (!string.IsNullOrWhiteSpace(mappingRule.FromParameter))
                    {
                        // 如果参数缺失，记录错误。
                        if (!SchemaMappingUtils.TryGetJobParameter(job.Parameters, mappingRule.FromParameter, out var paramValue))
                        {
                            errors.Add(CreateError(SchemaMappingErrorCode.JobParameterMissingForMapping, sourceTableId, targetTableId, mappingRule.FromParameter, null, null));
                            continue;
                        }

                        var mappingFromParam = new ColumnMapping(null, targetCol, MappingKind.Constant, paramValue);
                        var writabilityErrorParam = GetColumnWritabilityError(targetCol, mappingFromParam.Kind, tableTask.EnableIdentityInsert);
                        if (writabilityErrorParam.HasValue)
                        {
                            errors.Add(CreateError(writabilityErrorParam.Value, sourceTableId, targetTableId, targetCol.Name, null, null));
                            continue;
                        }
                        columnMappings.Add(mappingFromParam);
                        continue;
                    }

                    if (mappingRule.Const is not null)
                    {
                        var mappingConst = new ColumnMapping(null, targetCol, MappingKind.Constant, mappingRule.Const);
                        var writabilityErrorConst = GetColumnWritabilityError(targetCol, mappingConst.Kind, tableTask.EnableIdentityInsert);
                        if (writabilityErrorConst.HasValue)
                        {
                            errors.Add(CreateError(writabilityErrorConst.Value, sourceTableId, targetTableId, targetCol.Name, null, null));
                            continue;
                        }
                        columnMappings.Add(mappingConst);
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(mappingRule.FromSource))
                    {
                        if (!sourceCols.TryGetValue(mappingRule.FromSource!, out var userSourceCol))
                        {
                            errors.Add(CreateError(SchemaMappingErrorCode.TargetColumnMissingInSource, sourceTableId, targetTableId, targetCol.Name, null, targetCol.DbType));
                            continue;
                        }

                        if (!SchemaMappingUtils.IsTypeCompatible(userSourceCol, targetCol))
                        {
                            errors.Add(CreateError(SchemaMappingErrorCode.TargetColumnTypeIncompatible, sourceTableId, targetTableId, targetCol.Name, userSourceCol.DbType, targetCol.DbType));
                        }

                        var mappingFromSource = new ColumnMapping(userSourceCol, targetCol, MappingKind.OneToOne, null);
                        var writabilityErrorFromSource = GetColumnWritabilityError(targetCol, mappingFromSource.Kind, tableTask.EnableIdentityInsert);
                        if (writabilityErrorFromSource.HasValue)
                        {
                            errors.Add(CreateError(writabilityErrorFromSource.Value, sourceTableId, targetTableId, targetCol.Name, null, null));
                            continue;
                        }
                        columnMappings.Add(mappingFromSource);
                        continue;
                    }

                    // If a mapping rule exists but none of the actionable properties were set, treat as ignored.
                    columnMappings.Add(new ColumnMapping(null, targetCol, MappingKind.Ignored, null));
                    continue;
                }

                // No explicit rule — perform automatic name-based mapping if enabled.
                if (tableTask.ColumnMapping?.AutomapByName != false && sourceCols.TryGetValue(targetCol.Name, out var sourceCol))
                {
                    if (!SchemaMappingUtils.IsTypeCompatible(sourceCol, targetCol))
                    {
                        errors.Add(CreateError(SchemaMappingErrorCode.TargetColumnTypeIncompatible, sourceTableId, targetTableId, targetCol.Name, sourceCol.DbType, targetCol.DbType));
                    }
                    var mappingAuto = new ColumnMapping(sourceCol, targetCol, MappingKind.OneToOne, null);
                    var writabilityErrorAuto = GetColumnWritabilityError(targetCol, mappingAuto.Kind, tableTask.EnableIdentityInsert);
                    if (writabilityErrorAuto.HasValue)
                    {
                        errors.Add(CreateError(writabilityErrorAuto.Value, sourceTableId, targetTableId, targetCol.Name, null, null));
                        continue;
                    }
                    columnMappings.Add(mappingAuto);
                    continue;
                }

                if (schemaPolicy.AllowExtraTargetColumns && (targetCol.IsNullable || targetCol.IsIdentity || targetCol.IsComputed || targetCol.IsRowVersion))
                {
                    columnMappings.Add(new ColumnMapping(null, targetCol, MappingKind.Ignored, null));
                }
                else
                {
                    errors.Add(CreateError(SchemaMappingErrorCode.TargetColumnMissingInSource, sourceTableId, targetTableId, targetCol.Name, null, targetCol.DbType));
                }
            }

            if (schemaPolicy.RequirePrimaryKey)
            {
                if (targetTable.PrimaryKeyColumns.Count == 0)
                {
                    errors.Add(CreateError(SchemaMappingErrorCode.PrimaryKeyMissing, sourceTableId, targetTableId, null, null, null));
                }
            }

            var tableMapping = new TableMapping(sourceTableId, targetTableId, columnMappings, HasWarnings: false);
            tableMappings[targetTableId] = tableMapping;
        }

        bool success = errors.Count == 0;
        var resultValue = new SchemaMappingResult(success, errors, tableMappings);

        if (success)
            return Result<SchemaMappingResult>.Ok(resultValue);

        // Use the Fail overload that carries the result payload so callers can inspect errors programmatically.
        return Result<SchemaMappingResult>.Fail(resultValue, "Schema mapping failed");
    }

    /// <summary>
    /// Helper to create a <see cref="SchemaMappingError"/> with a standardized message template.
    /// </summary>
    private static SchemaMappingError CreateError(SchemaMappingErrorCode code, TableIdentifier? source, TableIdentifier? target, string? column, string? sourceType, string? targetType)
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

        return new SchemaMappingError(code, source, target, column, message, sourceType, targetType);
    }

    // Type compatibility and parameter helpers moved to SchemaMappingUtils.

    /// <summary>
    /// Check if a column is writable based on its schema properties.
    /// Returns a <see cref="SchemaMappingErrorCode"/> when the column cannot be written to,
    /// or null when the column is writable for the provided mapping kind and table settings.
    /// </summary>
    private static SchemaMappingErrorCode? GetColumnWritabilityError(
        ColumnSchema targetCol,
        MappingKind mappingKind,
        bool enableIdentityInsert)
    {
        // Ignored mappings never write, always safe
        if (mappingKind == MappingKind.Ignored)
            return null;

        // Computed columns cannot be written under any circumstance
        if (targetCol.IsComputed)
            return SchemaMappingErrorCode.ComputedColumnCannotBeWritten;

        // Rowversion/timestamp columns are auto-generated and cannot be written
        if (targetCol.IsRowVersion)
            return SchemaMappingErrorCode.RowVersionColumnCannotBeWritten;

        // Identity columns require explicit opt-in via EnableIdentityInsert
        if (targetCol.IsIdentity && !enableIdentityInsert)
            return SchemaMappingErrorCode.IdentityInsertNotEnabled;

        return null; // Column is writable
    }
}
