// ============================================================================
// File: IdentityInsertManager.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: Manages SET IDENTITY_INSERT ON/OFF scopes for SQL Server targets.
// ============================================================================

using System.Data;
using DotNetToolkit.Database.Abstractions;
using ReportSyncer.Core.Exceptions;
using ReportSyncer.Core.Sync.Contracts;
using ReportSyncer.Core.Sync.Sql;

namespace ReportSyncer.Core.Sync;

/// <summary>
/// Provides scoped management of SQL Server IDENTITY_INSERT setting for target tables.
/// Ensures IDENTITY_INSERT is turned OFF even when errors occur.
/// </summary>
public sealed class IdentityInsertManager
{
    private readonly Func<string, IDbContext> _dbContextFactory;

    public IdentityInsertManager(Func<string, IDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    /// <summary>
    /// Begins an IDENTITY_INSERT scope for the provided table context.
    /// </summary>
    /// <param name="tableCtx">Table context describing the target table and connection.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An async disposable scope that turns IDENTITY_INSERT OFF when disposed.</returns>
    /// <exception cref="SyncExecutionException">When enabling IDENTITY_INSERT fails.</exception>
    public async Task<IAsyncDisposable> BeginAsync(
        TableExecutionContext tableCtx,
        CancellationToken ct,
        IDbContext? dbOverride = null)
    {
        ArgumentNullException.ThrowIfNull(tableCtx);
        ct.ThrowIfCancellationRequested();

        if (!tableCtx.EnableIdentityInsert)
        {
            return NoopScope.Instance;
        }

        var ownsContext = dbOverride is null;
        var ctx = dbOverride ?? _dbContextFactory(tableCtx.TargetConnectionName)
                  ?? throw new SyncExecutionException($"Failed to resolve DB context for connection '{tableCtx.TargetConnectionName}'.");

        var qualifiedTable = SqlIdentifier.Qualify(tableCtx.TargetSchema, tableCtx.TargetTable);
        var onCommand = ctx.CreateCommand($"SET IDENTITY_INSERT {qualifiedTable} ON;", CommandType.Text);

        try
        {
            await ctx.ExecuteNonQueryAsync(onCommand, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (ownsContext) ctx.Dispose();
            throw;
        }
        catch (Exception ex)
        {
            if (ownsContext) ctx.Dispose();
            throw new SyncExecutionException(
                $"Failed to enable IDENTITY_INSERT for job '{tableCtx.JobName}' table '{qualifiedTable}'.",
                ex);
        }

        return new IdentityInsertScope(ctx, qualifiedTable, tableCtx.JobName, ownsContext);
    }

    private sealed class IdentityInsertScope : IAsyncDisposable
    {
        private readonly IDbContext _dbContext;
        private readonly string _qualifiedTable;
        private readonly string _jobName;
        private readonly bool _ownsContext;
        private bool _disposed;

        public IdentityInsertScope(IDbContext dbContext, string qualifiedTable, string jobName, bool ownsContext)
        {
            _dbContext = dbContext;
            _qualifiedTable = qualifiedTable;
            _jobName = jobName;
            _ownsContext = ownsContext;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                var offCommand = _dbContext.CreateCommand($"SET IDENTITY_INSERT {_qualifiedTable} OFF;", CommandType.Text);
                await _dbContext.ExecuteNonQueryAsync(offCommand, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new SyncExecutionException(
                    $"Failed to disable IDENTITY_INSERT for job '{_jobName}' table '{_qualifiedTable}'.",
                    ex);
            }
            finally
            {
                if (_ownsContext)
                    _dbContext.Dispose();
            }
        }
    }

    private sealed class NoopScope : IAsyncDisposable
    {
        public static readonly NoopScope Instance = new();
        private NoopScope() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
