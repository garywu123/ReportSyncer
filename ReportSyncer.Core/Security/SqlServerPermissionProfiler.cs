// ============================================================================
// File: SqlServerPermissionProfiler.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: SQL Server implementation of IPermissionProfiler using no-op probes.
// ============================================================================

using System.Collections.Generic;
using System.Data;
using System.Linq;
using DotNetToolkit.Database.Abstractions;
using Microsoft.Data.SqlClient;
using ReportSyncer.Core.Exceptions;
using ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Sync.Contracts;
using ReportSyncer.Core.Sync.Sql;

namespace ReportSyncer.Core.Security;

/// <summary>
/// Probes SQL Server permissions using no-op commands (SELECT TOP 0, DELETE WHERE 1=0, INSERT SELECT TOP 0).
/// Permission-denied errors are translated into <c>false</c> flags instead of exceptions; unexpected
/// errors are wrapped in <see cref="SyncExecutionException"/>.
/// </summary>
public sealed class SqlServerPermissionProfiler : IPermissionProfiler
{
    private readonly Func<string, IDbContext> _dbContextFactory;

    public SqlServerPermissionProfiler(Func<string, IDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    /// <inheritdoc />
    public async Task<PermissionsProfile> ProbeTablePermissionsAsync(TableExecutionContext tableCtx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tableCtx);
        ct.ThrowIfCancellationRequested();

        using var sourceDb = _dbContextFactory(tableCtx.SourceConnectionName);
        using var targetDb = _dbContextFactory(tableCtx.TargetConnectionName);

        var notes = new List<string>();

        var canReadSource = await ProbeReadAsync(
            sourceDb,
            tableCtx.SourceSchema,
            tableCtx.SourceTable,
            "source read",
            tableCtx,
            notes,
            ct).ConfigureAwait(false);

        var canReadTarget = await ProbeReadAsync(
            targetDb,
            tableCtx.TargetSchema,
            tableCtx.TargetTable,
            "target read",
            tableCtx,
            notes,
            ct).ConfigureAwait(false);

        if (tableCtx.DryRun)
        {
            notes.Add("Write permissions are not probed in DryRun.");
            return new PermissionsProfile(canReadSource, canReadTarget, null, null, null, ToReadOnlyNotes(notes));
        }

        bool? canDelete = null;
        if (tableCtx.PreSyncTargetDelete)
        {
            canDelete = await ProbeDeleteAsync(targetDb, tableCtx, notes, ct).ConfigureAwait(false);
        }
        else
        {
            notes.Add("Delete probe skipped because PreSyncTargetDelete=false.");
        }

        var canInsert = await ProbeInsertAsync(targetDb, tableCtx, notes, ct).ConfigureAwait(false);

        bool? canSetIdentityInsert = null;
        if (tableCtx.EnableIdentityInsert)
        {
            canSetIdentityInsert = await ProbeIdentityInsertAsync(targetDb, tableCtx, notes, ct).ConfigureAwait(false);
        }
        else
        {
            notes.Add("IDENTITY_INSERT probe skipped because EnableIdentityInsert=false.");
        }

        return new PermissionsProfile(
            canReadSource,
            canReadTarget,
            canDelete,
            canInsert,
            canSetIdentityInsert,
            ToReadOnlyNotes(notes));
    }

    private static async Task<bool> ProbeReadAsync(
        IDbContext db,
        string schema,
        string table,
        string operation,
        TableExecutionContext ctx,
        List<string> notes,
        CancellationToken ct)
    {
        var qualifiedTable = SqlIdentifier.Qualify(schema, table);
        var cmd = db.CreateCommand($"SELECT TOP (0) 1 FROM {qualifiedTable};", CommandType.Text);
        return await ExecuteProbeAsync(db, cmd, schema, table, operation, ctx, notes, ct).ConfigureAwait(false);
    }

    private static async Task<bool> ProbeDeleteAsync(
        IDbContext db,
        TableExecutionContext ctx,
        List<string> notes,
        CancellationToken ct)
    {
        var qualifiedTable = SqlIdentifier.Qualify(ctx.TargetSchema, ctx.TargetTable);
        var cmd = db.CreateCommand($"DELETE FROM {qualifiedTable} WHERE 1 = 0;", CommandType.Text);
        return await ExecuteProbeAsync(db, cmd, ctx.TargetSchema, ctx.TargetTable, "target delete", ctx, notes, ct)
            .ConfigureAwait(false);
    }

    private static async Task<bool> ProbeInsertAsync(
        IDbContext db,
        TableExecutionContext ctx,
        List<string> notes,
        CancellationToken ct)
    {
        var sql = BuildInsertProbeSql(ctx);
        var cmd = db.CreateCommand(sql, CommandType.Text);
        return await ExecuteProbeAsync(db, cmd, ctx.TargetSchema, ctx.TargetTable, "target insert", ctx, notes, ct)
            .ConfigureAwait(false);
    }

    private static async Task<bool> ProbeIdentityInsertAsync(
        IDbContext db,
        TableExecutionContext ctx,
        List<string> notes,
        CancellationToken ct)
    {
        var qualifiedTable = SqlIdentifier.Qualify(ctx.TargetSchema, ctx.TargetTable);
        var onCmd = db.CreateCommand($"SET IDENTITY_INSERT {qualifiedTable} ON;", CommandType.Text);

        var turnedOn = await ExecuteProbeAsync(
            db,
            onCmd,
            ctx.TargetSchema,
            ctx.TargetTable,
            "IDENTITY_INSERT ON",
            ctx,
            notes,
            ct).ConfigureAwait(false);

        if (!turnedOn)
            return false;

        var offCmd = db.CreateCommand($"SET IDENTITY_INSERT {qualifiedTable} OFF;", CommandType.Text);

        try
        {
            await db.ExecuteNonQueryAsync(offCmd, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            var message = $"Failed to restore IDENTITY_INSERT OFF for job '{ctx.JobName}' table '{ctx.TargetSchema}.{ctx.TargetTable}'.";
            throw new SyncExecutionException(message, ex);
        }

        return true;
    }

    private static async Task<bool> ExecuteProbeAsync(
        IDbContext db,
        IDbCommandWrapper command,
        string schema,
        string table,
        string operation,
        TableExecutionContext ctx,
        List<string> notes,
        CancellationToken ct)
    {
        try
        {
            await db.ExecuteNonQueryAsync(command, ct).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            if (IsPermissionDenied(ex))
            {
                notes.Add($"Permission denied for {operation} on {schema}.{table}.");
                return false;
            }

            var message = $"Failed to probe {operation} for job '{ctx.JobName}' table '{schema}.{table}'.";
            throw new SyncExecutionException(message, ex);
        }
    }

    private static string BuildInsertProbeSql(TableExecutionContext ctx)
    {
        var columns = BuildInsertColumns(ctx);
        if (columns.Count == 0)
        {
            throw new SyncExecutionException(
                $"Failed to build insert probe for job '{ctx.JobName}' table '{ctx.TargetSchema}.{ctx.TargetTable}': no target columns available.");
        }

        var qualifiedTable = SqlIdentifier.Qualify(ctx.TargetSchema, ctx.TargetTable);
        var columnList = string.Join(", ", columns.Select(SqlIdentifier.EscapeIdentifier));
        var values = string.Join(", ", columns.Select(_ => "NULL"));

        return $"INSERT INTO {qualifiedTable} ({columnList}) SELECT TOP (0) {values};";
    }

    private static IReadOnlyList<string> BuildInsertColumns(TableExecutionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx.TableMapping);
        var columns = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in ctx.TableMapping.ColumnMappings)
        {
            if (mapping.Kind == MappingKind.Ignored)
                continue;

            var targetColumn = mapping.TargetColumn;
            if (targetColumn.IsComputed || targetColumn.IsRowVersion)
                continue;

            if (seen.Add(targetColumn.Name))
            {
                columns.Add(targetColumn.Name);
            }
        }

        if (!string.IsNullOrWhiteSpace(ctx.ContextColumnName)
         && seen.Add(ctx.ContextColumnName))
        {
            columns.Add(ctx.ContextColumnName);
        }

        return columns;
    }

    private static bool IsPermissionDenied(Exception ex)
    {
        if (ex is SqlException sqlEx)
        {
            if (sqlEx.Errors.Cast<SqlError>().Any(e => e.Number == 229))
                return true;
        }

        if (!string.IsNullOrWhiteSpace(ex.Message)
         && ex.Message.IndexOf("permission", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        return ex.InnerException is not null && IsPermissionDenied(ex.InnerException);
    }

    private static IReadOnlyList<string> ToReadOnlyNotes(List<string> notes)
    {
        if (notes.Count == 0) return Array.Empty<string>();
        return notes.AsReadOnly();
    }
}
