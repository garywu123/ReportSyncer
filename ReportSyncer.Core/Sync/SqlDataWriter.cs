// ============================================================================
// File: SqlDataWriter.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: SQL Server implementation of IDataWriter with chunked delete and batched insert.
// ============================================================================

using System.Data;
using DotNetToolkit.Database.Abstractions;
using ReportSyncer.Core.Exceptions;
using ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Security;
using ReportSyncer.Core.Sync.Contracts;
using ReportSyncer.Core.Sync.Sql;

namespace ReportSyncer.Core.Sync;

/// <summary>
/// Executes delete and insert operations against a SQL Server target using DbContext abstractions.
/// </summary>
public sealed class SqlDataWriter : IDataWriter
{
    private readonly Func<string, IDbContext> _dbContextFactory;
    private readonly ISqlQueryBuilder _queryBuilder;
    private readonly IdentityInsertManager _identityInsertManager;

    public SqlDataWriter(
        Func<string, IDbContext> dbContextFactory,
        ISqlQueryBuilder queryBuilder,
        IdentityInsertManager identityInsertManager)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _queryBuilder = queryBuilder ?? throw new ArgumentNullException(nameof(queryBuilder));
        _identityInsertManager = identityInsertManager ?? throw new ArgumentNullException(nameof(identityInsertManager));
    }

    /// <inheritdoc />
    public async Task<int> DeleteAsync(TableExecutionContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ct.ThrowIfCancellationRequested();

        var filters = AppendContextFilter(ctx.Filters, ctx.ContextColumnName, ctx.ContextValue);
        var deleteCtx = new DeleteCommandContext(ctx.TargetSchema, ctx.TargetTable, filters);
        var spec = _queryBuilder.BuildDelete(deleteCtx);

        try
        {
            using var db = _dbContextFactory(ctx.TargetConnectionName);
            return await ExecuteNonQueryAsync(db, spec, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            var message = $"Failed to delete rows for job '{ctx.JobName}' table '{ctx.TargetSchema}.{ctx.TargetTable}'.";
            throw new SyncExecutionException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task<int> InsertAsync(
        TableExecutionContext ctx,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        IProgress<InsertBatchProgress>? progress,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(rows);
        ct.ThrowIfCancellationRequested();

        if (rows.Count == 0)
            return 0;

        var targetColumns = GetTargetColumns(ctx);
        var totalInserted = 0;

        using var db = _dbContextFactory(ctx.TargetConnectionName);

        try
        {
            for (var offset = 0; offset < rows.Count; offset += ctx.BatchSize)
            {
                ct.ThrowIfCancellationRequested();
                var batch = rows.Skip(offset).Take(ctx.BatchSize).ToList();
                var command = BuildInsertCommand(ctx, targetColumns, batch, db);
                var affected = await db.ExecuteNonQueryAsync(command, ct).ConfigureAwait(false);
                totalInserted += affected;

                // Report progress after each batch
                progress?.Report(new InsertBatchProgress(
                    TotalProcessed: totalInserted,
                    TotalPlanned: rows.Count,
                    BatchRowsAffected: affected));
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (SyncExecutionException) { throw; }
        catch (Exception ex)
        {
            var message = $"Failed to insert rows for job '{ctx.JobName}' table '{ctx.TargetSchema}.{ctx.TargetTable}'.";
            throw new SyncExecutionException(message, ex);
        }

        return totalInserted;
    }

    private static IDbCommandWrapper BuildInsertCommand(
        TableExecutionContext ctx,
        IReadOnlyList<string> targetColumns,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> batch,
        IDbContext db)
    {
        var qualifiedTable = SqlIdentifier.Qualify(ctx.TargetSchema, ctx.TargetTable);
        var columnList = string.Join(", ", targetColumns.Select(SqlIdentifier.EscapeIdentifier));

        var valuesClauses = new List<string>(batch.Count);
        var paramIndex = 0;
        var parameters = new List<CommandParameterSpec>();

        foreach (var row in batch)
        {
            var valuePlaceholders = new List<string>(targetColumns.Count);
            foreach (var column in targetColumns)
            {
                var name = $"@p{paramIndex++}";
                valuePlaceholders.Add(name);
                var value = ResolveValue(ctx, column, row);
                parameters.Add(new CommandParameterSpec(name, value, MapDbType(value)));
            }

            valuesClauses.Add($"({string.Join(", ", valuePlaceholders)})");
        }

        var sql = $"INSERT INTO {qualifiedTable} ({columnList}) VALUES {string.Join(", ", valuesClauses)};";
        if (ctx.EnableIdentityInsert)
        {
            sql = $"SET IDENTITY_INSERT {qualifiedTable} ON; {sql} SET IDENTITY_INSERT {qualifiedTable} OFF;";
        }
        var cmd = db.CreateCommand(sql, CommandType.Text);
        foreach (var p in parameters)
        {
            cmd.AddParameter(p.Name, p.Value ?? DBNull.Value, p.DbType ?? DbType.Object);
        }

        return cmd;
    }

    private static object? ResolveValue(
        TableExecutionContext ctx,
        string columnName,
        IReadOnlyDictionary<string, object?> row)
    {
        // Context column override
        if (!string.IsNullOrWhiteSpace(ctx.ContextColumnName)
            && columnName.Equals(ctx.ContextColumnName, StringComparison.OrdinalIgnoreCase))
        {
            return ctx.ContextValue;
        }

        // Mapping-based value resolution
        var mapping = ctx.TableMapping.ColumnMappings.FirstOrDefault(
            m => m.TargetColumn.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));

        if (mapping is null)
        {
            throw new SyncExecutionException($"Missing mapping for target column '{columnName}'.");
        }

        return mapping.Kind switch
        {
            MappingKind.Constant => mapping.ConstantValue,
            MappingKind.ContextColumn => ctx.ContextValue,
            _ => TryGetRowValue(row, columnName)
        };
    }

    private static object? TryGetRowValue(
        IReadOnlyDictionary<string, object?> row,
        string columnName)
    {
        foreach (var kv in row)
        {
            if (kv.Key.Equals(columnName, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        }

        throw new SyncExecutionException($"Missing value for target column '{columnName}'.");
    }

    private static IReadOnlyList<string> GetTargetColumns(TableExecutionContext ctx)
    {
        var columns = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in ctx.TableMapping.ColumnMappings)
        {
            if (mapping.Kind == MappingKind.Ignored)
                continue;
            if (mapping.TargetColumn.IsComputed || mapping.TargetColumn.IsRowVersion)
                continue;

            if (seen.Add(mapping.TargetColumn.Name))
                columns.Add(mapping.TargetColumn.Name);
        }

        if (!string.IsNullOrWhiteSpace(ctx.ContextColumnName) && seen.Add(ctx.ContextColumnName))
        {
            columns.Add(ctx.ContextColumnName);
        }

        return columns;
    }

    private static async Task<int> ExecuteNonQueryAsync(
        IDbContext db,
        DbCommandSpec spec,
        CancellationToken ct)
    {
            var cmd = db.CreateCommand(spec.Sql, CommandType.Text);
            foreach (var p in spec.Parameters)
            {
                cmd.AddParameter(p.Name, p.Value ?? DBNull.Value, p.DbType ?? DbType.Object);
            }

            return await db.ExecuteNonQueryAsync(cmd, ct).ConfigureAwait(false);
        }

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

    private static DbType MapDbType(object? value)
    {
        if (value is null) return DbType.Object;

        return value switch
        {
            string => DbType.String,
            int => DbType.Int32,
            long => DbType.Int64,
            short => DbType.Int16,
            byte => DbType.Byte,
            bool => DbType.Boolean,
            Guid => DbType.Guid,
            DateTime => DbType.DateTime,
            decimal => DbType.Decimal,
            double => DbType.Double,
            float => DbType.Single,
            _ => DbType.Object
        };
    }
}
