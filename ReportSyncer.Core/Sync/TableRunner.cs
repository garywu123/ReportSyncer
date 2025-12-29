// ============================================================================
// File: TableRunner.cs
// Author: Codex
// Project: ReportSyncer
// Description: Executes delete/insert operations for a single table sync.
// ============================================================================

using System.Data;
using System.Data.Common;
using DotNetToolkit.Database.Abstractions;
using ReportSyncer.Core.Exceptions;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Sync.Contracts;
using ReportSyncer.Core.Sync.Sql;

namespace ReportSyncer.Core.Sync;

/// <summary>
/// Coordinates delete and insert operations for a single table based on a <see cref="TableExecutionContext"/>.
/// </summary>
public sealed class TableRunner : ITableRunner
{
    private readonly IDbConnectionFactory _sourceConnectionFactory;
    private readonly IDataWriter _writer;
    private readonly ISqlQueryBuilder _sqlBuilder;

    public TableRunner(
        IDbConnectionFactory sourceConnectionFactory,
        IDataWriter writer,
        ISqlQueryBuilder sqlBuilder)
    {
        _sourceConnectionFactory = sourceConnectionFactory ?? throw new ArgumentNullException(nameof(sourceConnectionFactory));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _sqlBuilder = sqlBuilder ?? throw new ArgumentNullException(nameof(sqlBuilder));
    }

    /// <inheritdoc />
    public async Task<TableResult> RunAsync(TableExecutionContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ct.ThrowIfCancellationRequested();

        if (ctx.DryRun)
        {
            return new TableResult(
                ctx.TargetTable,
                TableStatus.SkippedDryRun,
                RowsDeleted: 0,
                RowsInserted: 0,
                Duration: TimeSpan.Zero,
                ErrorCode: null,
                ErrorMessage: "Dry-run: no changes were executed.");
        }

        var startedAt = DateTimeOffset.UtcNow;
        var phase = "Execute";

        try
        {
            var deleted = 0;
            if (ctx.PreSyncTargetDelete)
            {
                phase = "Delete";
                deleted = await _writer.DeleteAsync(ctx, ct).ConfigureAwait(false);
            }

            phase = "ReadSource";
            var rows = await ReadSourceRowsAsync(ctx, ct).ConfigureAwait(false);

            phase = "Insert";
            var inserted = rows.Count == 0
                ? 0
                : await _writer.InsertAsync(ctx, rows, ct).ConfigureAwait(false);

            var duration = DateTimeOffset.UtcNow - startedAt;
            return new TableResult(
                ctx.TargetTable,
                TableStatus.Succeeded,
                RowsDeleted: deleted,
                RowsInserted: inserted,
                Duration: duration);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SyncExecutionException ex)
        {
            throw new SyncExecutionException(BuildErrorMessage(ctx, phase), ex);
        }
        catch (Exception ex)
        {
            throw new SyncExecutionException(BuildErrorMessage(ctx, phase), ex);
        }
    }

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ReadSourceRowsAsync(
        TableExecutionContext ctx,
        CancellationToken ct)
    {
        try
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

            var select = _sqlBuilder.BuildSelectSource(insertCtx);

            await using var conn = (DbConnection)await _sourceConnectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = select.Sql;
            cmd.CommandType = CommandType.Text;

            foreach (var p in select.Parameters)
            {
                var dbParam = cmd.CreateParameter();
                dbParam.ParameterName = p.Name;
                dbParam.Value = p.Value ?? DBNull.Value;
                if (p.DbType.HasValue)
                    dbParam.DbType = p.DbType.Value;
                cmd.Parameters.Add(dbParam);
            }

            var rows = new List<IReadOnlyDictionary<string, object?>>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                rows.Add(MapRow(ctx, reader));
            }

            return rows.AsReadOnly();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var message = $"Failed to read source rows for job '{ctx.JobName}' table '{ctx.SourceSchema}.{ctx.SourceTable}'.";
            throw new SyncExecutionException(message, ex);
        }
    }

    private static IReadOnlyDictionary<string, object?> MapRow(TableExecutionContext ctx, DbDataReader reader)
    {
        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in ctx.TableMapping.ColumnMappings)
        {
            if (mapping.Kind == MappingKind.Ignored)
                continue;

            var targetName = mapping.TargetColumn.Name;
            object? value = mapping.Kind switch
            {
                MappingKind.OneToOne => GetValue(reader, mapping.SourceColumn),
                MappingKind.Constant => mapping.ConstantValue,
                MappingKind.ContextColumn => ctx.ContextValue,
                _ => null
            };

            row[targetName] = value;
        }

        if (!string.IsNullOrWhiteSpace(ctx.ContextColumnName) &&
            !row.ContainsKey(ctx.ContextColumnName))
        {
            row[ctx.ContextColumnName] = ctx.ContextValue;
        }

        return row;
    }

    private static object? GetValue(DbDataReader reader, ColumnSchema? sourceColumn)
    {
        if (sourceColumn is null)
        {
            throw new SyncExecutionException("Source column mapping is missing for a one-to-one target column.");
        }

        var raw = reader[sourceColumn.Name];
        return raw is DBNull ? null : raw;
    }

    private static string BuildErrorMessage(TableExecutionContext ctx, string phase)
    {
        return $"Failed to execute job '{ctx.JobName}' for table '{ctx.TargetSchema}.{ctx.TargetTable}' during phase '{phase}'.";
    }
}
