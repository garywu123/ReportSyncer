// ============================================================================
// File: WorkEstimator.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: Estimates rows to delete/insert for a table sync using COUNT queries.
// ============================================================================

using System.Data;
using DotNetToolkit.Database.Abstractions;
using ReportSyncer.Core.Sync.Contracts;
using ReportSyncer.Core.Sync.Sql;
using System.Data.Common;

namespace ReportSyncer.Core.Observability;

/// <summary>
/// Estimates sync work (delete and insert counts) without performing any data-modifying
/// operations. This class uses the provided <see cref="ISqlQueryBuilder"/> to build
/// counting SQL and executes those queries via the supplied <see cref="IDbConnectionFactory"/>
/// instances to produce a <see cref="WorkEstimate"/> for a given <see cref="TableExecutionContext"/>.
/// </summary>
public sealed class WorkEstimator
{
    private readonly IDbConnectionFactory _sourceConnectionFactory;
    private readonly IDbConnectionFactory _targetConnectionFactory;
    private readonly ISqlQueryBuilder _sqlBuilder;

    public WorkEstimator(
        IDbConnectionFactory sourceConnectionFactory,
        IDbConnectionFactory targetConnectionFactory,
        ISqlQueryBuilder sqlBuilder)
    {
        _sourceConnectionFactory = sourceConnectionFactory ?? throw new ArgumentNullException(nameof(sourceConnectionFactory));
        _targetConnectionFactory = targetConnectionFactory ?? throw new ArgumentNullException(nameof(targetConnectionFactory));
        _sqlBuilder = sqlBuilder ?? throw new ArgumentNullException(nameof(sqlBuilder));
    }

    /// <summary>
    /// Produces a <see cref="WorkEstimate"/> describing estimated rows to insert and
    /// (optionally) rows to delete for the provided table execution context.
    /// </summary>
    /// <param name="ctx">Execution context describing source/target tables, mappings and options.</param>
    /// <param name="ct">Cancellation token that aborts long-running operations.</param>
    /// <returns>A <see cref="WorkEstimate"/> containing counts, optional delete statistics and warnings.</returns>
    /// <exception cref="WorkEstimationException">Wraps unexpected exceptions raised during estimation.</exception>
    public async Task<WorkEstimate> EstimateAsync(TableExecutionContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ct.ThrowIfCancellationRequested();

        try
        {
            var warnings = new List<string>();
            var rowsToInsert = await CountSourceAsync(ctx, ct).ConfigureAwait(false);

            long rowsToDelete = 0;
            EstimatedDeleteStats? deleteStats = null;
            double? deletePct = null;

            if (ctx.PreSyncTargetDelete)
            {
                var deleteFilters = AppendContextFilter(ctx.Filters, ctx.ContextColumnName, ctx.ContextValue);
                rowsToDelete = await CountTargetAsync(ctx, deleteFilters, ct).ConfigureAwait(false);
                var totalRows = await CountTargetAsync(ctx, Array.Empty<FilterPredicate>(), ct).ConfigureAwait(false);

                if (totalRows == 0)
                {
                    deletePct = null;
                    if (rowsToDelete > 0)
                    {
                        warnings.Add("Delete estimate greater than zero while total rows is zero.");
                    }
                }
                else
                {
                    deletePct = (double)rowsToDelete / totalRows * 100d;
                }

                deleteStats = new EstimatedDeleteStats(totalRows, rowsToDelete, deletePct);

                if (deleteFilters.Count == 0)
                {
                    warnings.Add("Delete scope has no predicates; this may delete the entire table.");
                }
            }

            return new WorkEstimate(rowsToDelete, rowsToInsert, deletePct, deleteStats, warnings.AsReadOnly());
        }
        catch (Exception ex) when (ex is not WorkEstimationException)
        {
            var message = $"Failed to estimate work for job '{ctx.JobName}' table '{ctx.TargetSchema}.{ctx.TargetTable}' during phase '{SyncPhase.Estimate}'.";
            throw new WorkEstimationException(message, ex);
        }
    }

    /// <summary>
    /// Counts the number of source rows that would be read for an insert operation.
    /// It builds a SELECT using the <see cref="ISqlQueryBuilder.BuildSelectSource"/> and
    /// executes a COUNT wrapper around that SELECT on the source connection.
    /// </summary>
    /// <param name="ctx">Table execution context describing the source mapping and filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of rows on the source that match the select criteria.</returns>
    private async Task<long> CountSourceAsync(TableExecutionContext ctx, CancellationToken ct)
    {
        var insertCtx = new InsertCommandContext(
            ctx.SourceSchema,
            ctx.SourceTable,
            ctx.TargetSchema,
            ctx.TargetTable,
            ctx.Filters,
            ctx.ContextColumnName,
            ctx.ContextValue,
            ctx.TableMapping,
            ctx.EnableIdentityInsert,
            ctx.BatchSize);

        var selectSpec = _sqlBuilder.BuildSelectSource(insertCtx);
        var countSql = $"SELECT COUNT(1) FROM ({selectSpec.Sql}) AS src";

        return await ExecuteScalarAsync<long>(_sourceConnectionFactory, countSql, selectSpec.Parameters, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Counts rows on the target using the SQL produced by
    /// <see cref="ISqlQueryBuilder.BuildCountEstimate"/> for a context cloned with the
    /// provided filters.
    /// </summary>
    /// <param name="ctx">Original table execution context used as a base for cloning.</param>
    /// <param name="filters">Filters to apply when counting on the target.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The row count on the target matching the filters.</returns>
    private async Task<long> CountTargetAsync(TableExecutionContext ctx, IReadOnlyList<FilterPredicate> filters, CancellationToken ct)
    {
        var countSpec = _sqlBuilder.BuildCountEstimate(CloneWithFilters(ctx, filters));
        return await ExecuteScalarAsync<long>(_targetConnectionFactory, countSpec.Sql, countSpec.Parameters, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Returns a shallow clone of <paramref name="ctx"/> with the provided filters replaced.
    /// This is used to build count SQL that applies a different set of predicates than the
    /// original execution context.
    /// </summary>
    private static TableExecutionContext CloneWithFilters(TableExecutionContext ctx, IReadOnlyList<FilterPredicate> filters)
    {
        return new TableExecutionContext(
            ctx.JobId,
            ctx.JobName,
            ctx.SourceConnectionName,
            ctx.TargetConnectionName,
            ctx.SourceDbType,
            ctx.TargetDbType,
            ctx.SourceSchema,
            ctx.SourceTable,
            ctx.TargetSchema,
            ctx.TargetTable,
            ctx.DryRun,
            ctx.PreSyncTargetDelete,
            ctx.EnableIdentityInsert,
            ctx.ContextColumnName,
            ctx.ContextValue,
            filters,
            ctx.TableMapping,
            ctx.ExecutionPlan,
            ctx.BatchSize,
            ctx.EtaSmoothing);
    }

    /// <summary>
    /// Executes a scalar SQL query using the provided <see cref="IDbConnectionFactory"/>,
    /// mapping <see cref="CommandParameterSpec"/> entries to ADO.NET parameters.
    /// </summary>
    /// <typeparam name="T">Expected CLR type of the scalar result.</typeparam>
    /// <param name="connectionFactory">Factory to create an open DB connection.</param>
    /// <param name="sql">SQL text to execute.</param>
    /// <param name="parameters">Ordered list of parameters to add to the command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The scalar result converted to <typeparamref name="T"/>, or default if null/DBNULL.</returns>
    private static async Task<T> ExecuteScalarAsync<T>(IDbConnectionFactory connectionFactory, string sql, IReadOnlyList<CommandParameterSpec> parameters, CancellationToken ct)
    {
        await using var conn = (DbConnection)await connectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandType = CommandType.Text;

        foreach (var p in parameters)
        {
            var dbParam = cmd.CreateParameter();
            dbParam.ParameterName = p.Name;
            dbParam.Value = p.Value ?? DBNull.Value;
            if (p.DbType.HasValue)
                dbParam.DbType = p.DbType.Value;
            cmd.Parameters.Add(dbParam);
        }

        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        if (result is null || result is DBNull)
            return default!;

        return (T)Convert.ChangeType(result, typeof(T));
    }

    /// <summary>
    /// Returns the original filter list with an additional equality predicate for the
    /// configured context column/value, if a context column is supplied. If
    /// <paramref name="contextColumnName"/> is null or whitespace, the original list is returned.
    /// </summary>
    private static IReadOnlyList<FilterPredicate> AppendContextFilter(
        IReadOnlyList<FilterPredicate> filters,
        string? contextColumnName,
        object? contextValue)
    {
        if (string.IsNullOrWhiteSpace(contextColumnName))
            return filters;

        var list = new List<FilterPredicate>(filters.Count + 1);
        list.AddRange(filters);
        list.Add(new FilterPredicate(contextColumnName, FilterOperator.Equals, contextValue));
        return list.AsReadOnly();
    }
}
