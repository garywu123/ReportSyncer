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
using ReportSyncer.Core.Observability;
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
    private readonly IJobProgressReporter _progress;

    public TableRunner(
        IDbConnectionFactory sourceConnectionFactory,
        IDataWriter writer,
        ISqlQueryBuilder sqlBuilder,
        IJobProgressReporter? progressReporter = null)
    {
        _sourceConnectionFactory = sourceConnectionFactory ?? throw new ArgumentNullException(nameof(sourceConnectionFactory));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _sqlBuilder = sqlBuilder ?? throw new ArgumentNullException(nameof(sqlBuilder));
        _progress = progressReporter ?? NullJobProgressReporter.Instance;
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

    /// <inheritdoc />
    public async Task<TableResult> RunPhaseAsync(TableExecutionContext ctx, SyncPhase phase, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ct.ThrowIfCancellationRequested();

        if (phase == SyncPhase.Delete)
        {
            var startedAt = DateTimeOffset.UtcNow;
            _progress.Report(new TableProgressEvent(
                ctx.JobName,
                TableIdentifier.Parse($"{ctx.TargetSchema}.{ctx.TargetTable}"),
                ProgressEventKind.Started,
                SyncPhase.Delete,
                startedAt,
                IsDryRun: ctx.DryRun));

            if (ctx.DryRun || !ctx.PreSyncTargetDelete)
            {
                _progress.Report(new TableProgressEvent(
                    ctx.JobName,
                    TableIdentifier.Parse($"{ctx.TargetSchema}.{ctx.TargetTable}"),
                    ProgressEventKind.Skipped,
                    SyncPhase.Delete,
                    DateTimeOffset.UtcNow,
                    Elapsed: DateTimeOffset.UtcNow - startedAt,
                    RowsAffected: 0,
                    IsDryRun: ctx.DryRun,
                    Message: ctx.DryRun ? "Dry-run: delete skipped." : "Delete skipped by configuration."));

                return new TableResult(
                    ctx.TargetTable,
                    ctx.DryRun ? TableStatus.SkippedDryRun : TableStatus.Succeeded,
                    RowsDeleted: 0,
                    RowsInserted: null,
                    Duration: TimeSpan.Zero);
            }

            var started = DateTimeOffset.UtcNow;
            try
            {
                var deleted = await _writer.DeleteAsync(ctx, ct).ConfigureAwait(false);
                var finished = DateTimeOffset.UtcNow;
                _progress.Report(new TableProgressEvent(
                    ctx.JobName,
                    TableIdentifier.Parse($"{ctx.TargetSchema}.{ctx.TargetTable}"),
                    ProgressEventKind.Completed,
                    SyncPhase.Delete,
                    finished,
                    Elapsed: finished - started,
                    RowsAffected: deleted,
                    IsDryRun: ctx.DryRun));

                var duration = finished - started;
                return new TableResult(ctx.TargetTable, TableStatus.Succeeded, RowsDeleted: deleted, Duration: duration);
            }
            catch (OperationCanceledException) { _progress.Report(BuildFailedEvent(ctx, SyncPhase.Delete, startedAt, "Cancelled")); throw; }
            catch (SyncExecutionException ex) { _progress.Report(BuildFailedEvent(ctx, SyncPhase.Delete, startedAt, ex.Message)); throw new SyncExecutionException(BuildErrorMessage(ctx, "Delete"), ex); }
            catch (Exception ex) { _progress.Report(BuildFailedEvent(ctx, SyncPhase.Delete, startedAt, ex.Message)); throw new SyncExecutionException(BuildErrorMessage(ctx, "Delete"), ex); }
        }

        if (phase == SyncPhase.Insert)
        {
            var started = DateTimeOffset.UtcNow;
            _progress.Report(new TableProgressEvent(
                ctx.JobName,
                TableIdentifier.Parse($"{ctx.TargetSchema}.{ctx.TargetTable}"),
                ProgressEventKind.Started,
                SyncPhase.Insert,
                started,
                IsDryRun: ctx.DryRun));
            try
            {
                if (ctx.DryRun)
                {
                    _progress.Report(new TableProgressEvent(
                        ctx.JobName,
                        TableIdentifier.Parse($"{ctx.TargetSchema}.{ctx.TargetTable}"),
                        ProgressEventKind.Completed,
                        SyncPhase.Insert,
                        DateTimeOffset.UtcNow,
                        Elapsed: DateTimeOffset.UtcNow - started,
                        RowsAffected: 0,
                        IsDryRun: true,
                        Message: "Dry-run: no inserts executed."));

                    return new TableResult(ctx.TargetTable, TableStatus.SkippedDryRun, RowsInserted: 0, Duration: TimeSpan.Zero);
                }

                var rows = await ReadSourceRowsAsync(ctx, ct).ConfigureAwait(false);
                var inserted = rows.Count == 0
                    ? 0
                    : await _writer.InsertAsync(ctx, rows, ct).ConfigureAwait(false);

                var finished = DateTimeOffset.UtcNow;
                _progress.Report(new TableProgressEvent(
                    ctx.JobName,
                    TableIdentifier.Parse($"{ctx.TargetSchema}.{ctx.TargetTable}"),
                    ProgressEventKind.Completed,
                    SyncPhase.Insert,
                    finished,
                    Elapsed: finished - started,
                    RowsAffected: inserted,
                    IsDryRun: ctx.DryRun));

                var duration = finished - started;
                return new TableResult(ctx.TargetTable, TableStatus.Succeeded, RowsInserted: inserted, Duration: duration);
            }
            catch (OperationCanceledException) { _progress.Report(BuildFailedEvent(ctx, SyncPhase.Insert, started, "Cancelled")); throw; }
            catch (SyncExecutionException ex) { _progress.Report(BuildFailedEvent(ctx, SyncPhase.Insert, started, ex.Message)); throw new SyncExecutionException(BuildErrorMessage(ctx, "Insert"), ex); }
            catch (Exception ex) { _progress.Report(BuildFailedEvent(ctx, SyncPhase.Insert, started, ex.Message)); throw new SyncExecutionException(BuildErrorMessage(ctx, "Insert"), ex); }
        }

        throw new SyncExecutionException(BuildErrorMessage(ctx, phase.ToString()));
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

    private TableProgressEvent BuildFailedEvent(TableExecutionContext ctx, SyncPhase phase, DateTimeOffset startedAt, string message)
    {
        var now = DateTimeOffset.UtcNow;
        return new TableProgressEvent(
            ctx.JobName,
            TableIdentifier.Parse($"{ctx.TargetSchema}.{ctx.TargetTable}"),
            ProgressEventKind.Failed,
            phase,
            now,
            Elapsed: now - startedAt,
            IsDryRun: ctx.DryRun,
            ErrorMessage: message);
    }
}
