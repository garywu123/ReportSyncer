// ============================================================================
// File: SqlServerQueryBuilder.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: SQL Server implementation of ISqlQueryBuilder.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Core.Sync.Sql;

/// <summary>
/// SQL Server implementation of <see cref="ISqlQueryBuilder"/>.
/// Produces platform-specific SQL command text and parameter specifications for sync operations.
/// </summary>
public sealed class SqlServerQueryBuilder : ISqlQueryBuilder
{
    private readonly WhereClauseBuilder _whereClauseBuilder = new();

    /// <summary>
    /// Builds a <c>SELECT COUNT(1)</c> command for the provided table execution context.
    /// </summary>
    /// <param name="ctx">Table execution context containing target schema/table and optional filters.</param>
    /// <returns>A <see cref="DbCommandSpec"/> that returns the count when executed.</returns>
    public DbCommandSpec BuildCountEstimate(TableExecutionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var qualifiedTable = SqlIdentifier.Qualify(ctx.TargetSchema, ctx.TargetTable);
        var (whereSql, parameters) = _whereClauseBuilder.Build(ctx.Filters);

        var sql = $"SELECT COUNT(1) FROM {qualifiedTable}";
        if (!string.IsNullOrWhiteSpace(whereSql))
        {
            sql += $" {whereSql}";
        }

        return new DbCommandSpec(sql, parameters);
    }

    /// <summary>
    /// Builds a DELETE statement for the target table using the provided filters.
    /// </summary>
    /// <param name="ctx">Delete command context containing target table and filter predicates.</param>
    /// <returns>A <see cref="DbCommandSpec"/> representing the DELETE statement and parameters.</returns>
    /// <exception cref="ConfigurationException">If no filters are provided in <paramref name="ctx"/>.</exception>
    public DbCommandSpec BuildDelete(DeleteCommandContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (ctx.Filters.Count == 0)
            throw new ConfigurationException("Delete operations must include at least one filter.");

        var qualifiedTable = SqlIdentifier.Qualify(ctx.TargetSchema, ctx.TargetTable);
        var (whereSql, parameters) = _whereClauseBuilder.Build(ctx.Filters);

        var sql = $"DELETE FROM {qualifiedTable} {whereSql}";
        return new DbCommandSpec(sql, parameters);
    }

    /// <summary>
    /// Builds a SELECT statement to read source rows to be inserted into the target.
    /// </summary>
    /// <param name="ctx">Insert command context describing source table, mapping and filters.</param>
    /// <returns>A <see cref="DbCommandSpec"/> that selects the required source columns.</returns>
    public DbCommandSpec BuildSelectSource(InsertCommandContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var sourceTable = SqlIdentifier.Qualify(ctx.SourceSchema, ctx.SourceTable);
        var columns = GetSourceColumns(ctx.TableMapping);

        var selectList = string.Join(", ", columns.Select(SqlIdentifier.EscapeIdentifier));
        var (whereSql, parameters) = _whereClauseBuilder.Build(ctx.Filters);

        var sql = $"SELECT {selectList} FROM {sourceTable}";
        if (!string.IsNullOrWhiteSpace(whereSql))
        {
            sql += $" {whereSql}";
        }

        return new DbCommandSpec(sql, parameters);
    }

    /// <summary>
    /// Builds an INSERT statement for the target table using the provided mapping and context values.
    /// </summary>
    /// <param name="ctx">Insert command context describing the target, mapping and optional context column.</param>
    /// <returns>A <see cref="DbCommandSpec"/> representing the INSERT statement and its parameters.</returns>
    public DbCommandSpec BuildInsertTarget(InsertCommandContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var targetTable = SqlIdentifier.Qualify(ctx.TargetSchema, ctx.TargetTable);
        var targetColumns = GetTargetColumns(ctx.TableMapping);

        var columns = new List<string>(targetColumns);
        var parameters = new List<CommandParameterSpec>();
        var values = new List<string>();
        var index = 0;

        foreach (var mapping in ctx.TableMapping.ColumnMappings.Where(m => m.Kind != MappingKind.Ignored))
        {
            var parameterName = $"@p{index}";
            if (mapping.Kind == MappingKind.Constant)
            {
                parameters.Add(new CommandParameterSpec(parameterName, mapping.ConstantValue));
            }
            else if (mapping.Kind == MappingKind.ContextColumn)
            {
                parameters.Add(new CommandParameterSpec(parameterName, ctx.ContextValue));
            }
            else
            {
                parameters.Add(new CommandParameterSpec(parameterName, null));
            }

            values.Add(parameterName);
            index++;
        }

        if (ctx.ContextColumnName is not null && !columns.Contains(ctx.ContextColumnName, StringComparer.OrdinalIgnoreCase))
        {
            columns.Add(ctx.ContextColumnName);
            var parameterName = $"@p{index}";
            parameters.Add(new CommandParameterSpec(parameterName, ctx.ContextValue));
            values.Add(parameterName);
        }

        var columnList = string.Join(", ", columns.Select(SqlIdentifier.EscapeIdentifier));
        var valuesList = string.Join(", ", values);

        var sql = $"INSERT INTO {targetTable} ({columnList}) VALUES ({valuesList})";
        return new DbCommandSpec(sql, parameters.AsReadOnly());
    }

    /// <summary>
    /// Returns the source column names for one-to-one mappings from the provided <see cref="TableMapping"/>.
    /// </summary>
    /// <param name="mapping">The table mapping to inspect.</param>
    /// <returns>List of source column names used in the SELECT list.</returns>
    private static IReadOnlyList<string> GetSourceColumns(TableMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        return mapping.ColumnMappings
            .Where(m => m.Kind == MappingKind.OneToOne && m.SourceColumn is not null)
            .Select(m => m.SourceColumn!.Name)
            .ToArray();
    }

    /// <summary>
    /// Returns the target column names for mappings that are not ignored.
    /// </summary>
    /// <param name="mapping">The table mapping to inspect.</param>
    /// <returns>List of target column names for the INSERT statement.</returns>
    private static IReadOnlyList<string> GetTargetColumns(TableMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        return mapping.ColumnMappings
            .Where(m => m.Kind != MappingKind.Ignored)
            .Select(m => m.TargetColumn.Name)
            .ToArray();
    }
}
