// ============================================================================
// File: SyncOrchestrator.cs
// Author: Codex
// Project: ReportSyncer
// Date: 2025-12-26
// Description: Orchestrates sync job execution with preflight and dry-run flow.
// ============================================================================

using System.Globalization;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Exceptions;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Observability;
using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Core.Sync;

/// <summary>
/// Coordinates sync execution by loading configuration, resolving the job, running preflight,
/// and dispatching execution (dry-run or real run).
/// </summary>
public sealed class SyncOrchestrator : ISyncOrchestrator
{
    private readonly IConfigurationProvider _configurationProvider;
    private readonly IPreFlightValidator _preFlightValidator;
    private readonly ITableRunner _tableRunner;
    private readonly IJobProgressReporter _progress;
    /// <summary>
    /// Initializes a new instance of the <see cref="SyncOrchestrator"/> class.
    /// </summary>
    /// <param name="configurationProvider">Provides configuration loading and validation.</param>
    /// <param name="preFlightValidator">Validates jobs before execution.</param>
    /// <param name="tableRunner">Executes per-table delete/insert phases.</param>
    /// <param name="progressReporter">Optional progress reporter sink.</param>
    public SyncOrchestrator(
        IConfigurationProvider configurationProvider,
        IPreFlightValidator preFlightValidator,
        ITableRunner tableRunner,
        IJobProgressReporter? progressReporter = null)
    {
        _configurationProvider = configurationProvider ?? throw new ArgumentNullException(nameof(configurationProvider));
        _preFlightValidator = preFlightValidator ?? throw new ArgumentNullException(nameof(preFlightValidator));
        _tableRunner = tableRunner ?? throw new ArgumentNullException(nameof(tableRunner));
        _progress = progressReporter ?? NullJobProgressReporter.Instance;
    }

    /// <summary>
    /// Runs the named job using configuration loaded from <paramref name="configPath"/>.
    /// This method performs configuration loading and validation, resolves the job,
    /// runs pre-flight validation and either returns a dry-run result or invokes the
    /// execution engine (not implemented in this component).
    /// </summary>
    /// <param name="configPath">Path to the configuration file.</param>
    /// <param name="jobName">The name of the job to execute (case-insensitive).</param>
    /// <param name="overrides">Optional runtime overrides applied when loading configuration.</param>
    /// <param name="ct">A <see cref="CancellationToken"/> used to cancel the operation.</param>
    /// <returns>A <see cref="JobResult"/> describing the outcome of the job run.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="configPath"/> or <paramref name="jobName"/> is invalid.</exception>
    /// <exception cref="ConfigurationException">Thrown when the named job cannot be found in configuration.</exception>
    /// <exception cref="SyncExecutionException">Thrown when execution fails or is not implemented.</exception>
    public async Task<JobResult> RunJobAsync(
        string configPath,
        string jobName,
        RuntimeOverrides? overrides,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            throw new ArgumentException("Configuration path cannot be null or whitespace.", nameof(configPath));
        }

        if (string.IsNullOrWhiteSpace(jobName))
        {
            throw new ArgumentException("Job name cannot be null or whitespace.", nameof(jobName));
        }

        ct.ThrowIfCancellationRequested();

        var startedAt = DateTimeOffset.UtcNow;
        _progress.Report(new JobProgressEvent(jobName, ProgressEventKind.Started, ProgressPhase.Preflight, startedAt));

        try
        {
            var effectiveConfig = await _configurationProvider
                .LoadAndValidateAsync(configPath, overrides, ct)
                .ConfigureAwait(false);

            var job = effectiveConfig.SyncJobs.FirstOrDefault(j =>
                string.Equals(j.Name, jobName, StringComparison.OrdinalIgnoreCase)) ?? throw new ConfigurationException(
                    string.Format(CultureInfo.InvariantCulture, "Job '{0}' was not found in configuration.", jobName));

            var preFlight = await _preFlightValidator
                .ValidateAsync(effectiveConfig, job, ct)
                .ConfigureAwait(false);
            _progress.Report(new JobProgressEvent(job.Name, ProgressEventKind.Completed, ProgressPhase.Preflight, DateTimeOffset.UtcNow, Elapsed: DateTimeOffset.UtcNow - startedAt));

            var dryRun = preFlight.DryRun;
            if (dryRun)
            {
                var tableResults = job.Tables
                    .Select(t => new TableResult(t.Target, TableStatus.SkippedDryRun))
                    .ToArray();
                _progress.Report(new JobProgressEvent(job.Name, ProgressEventKind.Completed, ProgressPhase.Execution, DateTimeOffset.UtcNow, Elapsed: DateTimeOffset.UtcNow - startedAt, Message: "Dry-run"));

                return new JobResult(
                    job.Name,
                    JobStatus.SkippedDryRun,
                    dryRun: true,
                    startedAtUtc: startedAt,
                    finishedAtUtc: DateTimeOffset.UtcNow,
                    tables: tableResults);
            }

            var execStarted = DateTimeOffset.UtcNow;
            _progress.Report(new JobProgressEvent(job.Name, ProgressEventKind.Started, ProgressPhase.Execution, execStarted));

            var results = new List<TableResult>();

            foreach (var tableId in preFlight.Schema.ExecutionPlan.DeleteOrder)
            {
                var ctx = BuildTableExecutionContext(job, tableId, effectiveConfig, preFlight);
                var tableResult = await _tableRunner.RunPhaseAsync(ctx, SyncPhase.Delete, ct).ConfigureAwait(false);
                results.Add(tableResult);
            }

            foreach (var tableId in preFlight.Schema.ExecutionPlan.InsertOrder)
            {
                var ctx = BuildTableExecutionContext(job, tableId, effectiveConfig, preFlight);
                var tableResult = await _tableRunner.RunPhaseAsync(ctx, SyncPhase.Insert, ct).ConfigureAwait(false);
                results.Add(tableResult);
            }

            _progress.Report(new JobProgressEvent(job.Name, ProgressEventKind.Completed, ProgressPhase.Execution, DateTimeOffset.UtcNow, Elapsed: DateTimeOffset.UtcNow - execStarted));
            return new JobResult(
                job.Name,
                JobStatus.Succeeded,
                dryRun: false,
                startedAtUtc: startedAt,
                finishedAtUtc: DateTimeOffset.UtcNow,
                tables: results.AsReadOnly());
        }
        catch (OperationCanceledException)
        {
            _progress.Report(new JobProgressEvent(jobName, ProgressEventKind.Failed, ProgressPhase.Execution, DateTimeOffset.UtcNow, Elapsed: DateTimeOffset.UtcNow - startedAt, ErrorMessage: "Cancelled"));
            throw;
        }
        catch (Exception ex)
        {
            _progress.Report(new JobProgressEvent(jobName, ProgressEventKind.Failed, ProgressPhase.Execution, DateTimeOffset.UtcNow, Elapsed: DateTimeOffset.UtcNow - startedAt, ErrorMessage: ex.Message));
            throw;
        }
    }

    private static TableExecutionContext BuildTableExecutionContext(
        SyncJobConfig job,
        TableIdentifier targetTableId,
        SyncConfiguration config,
        PreFlightResult preFlight)
    {
        var tableTask = job.Tables.FirstOrDefault(t =>
            string.Equals(t.Target, targetTableId.ToString(), StringComparison.OrdinalIgnoreCase));

        tableTask ??= job.Tables.FirstOrDefault(t =>
            string.Equals(TableIdentifier.Parse(t.Target).TableName, targetTableId.TableName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(TableIdentifier.Parse(t.Target).SchemaName, targetTableId.SchemaName, StringComparison.OrdinalIgnoreCase));

        if (tableTask is null)
        {
            throw new SyncExecutionException($"Table task for target '{targetTableId}' was not found.");
        }

        if (!preFlight.Schema.Mapping.TableMappings.TryGetValue(targetTableId, out var mapping))
        {
            throw new SyncExecutionException($"Mapping for target '{targetTableId}' was not found.");
        }

        var sourceId = TableIdentifier.Parse(tableTask.Source);
        var targetId = TableIdentifier.Parse(tableTask.Target);
        var filters = BuildFilters(tableTask.Filter, job);

        return new TableExecutionContext(
            Guid.NewGuid(),
            job.Name,
            job.SourceConnection,
            job.TargetConnection,
            DatabaseType.SqlServer,
            DatabaseType.SqlServer,
            sourceId.SchemaName,
            sourceId.TableName,
            targetId.SchemaName,
            targetId.TableName,
            dryRun: config.Run.DryRun,
            preSyncTargetDelete: tableTask.PreSyncTargetAction,
            enableIdentityInsert: tableTask.EnableIdentityInsert,
            contextColumnName: null,
            contextValue: null,
            filters: filters,
            mapping,
            preFlight.Schema.ExecutionPlan,
            config.Run.DefaultBatchSize,
            config.Run.EtaSmoothing);
    }

    private static IReadOnlyList<FilterPredicate> BuildFilters(FilterConfig? filterConfig, SyncJobConfig job)
    {
        if (filterConfig is null)
            return Array.Empty<FilterPredicate>();

        var list = new List<FilterPredicate>();
        if (!string.IsNullOrWhiteSpace(filterConfig.KeyColumn) && !string.IsNullOrWhiteSpace(filterConfig.Value))
        {
            list.Add(new FilterPredicate(
                filterConfig.KeyColumn,
                FilterOperator.Equals,
                ResolvePlaceholder(filterConfig.Value, job)));
        }

        if (!string.IsNullOrWhiteSpace(filterConfig.DateColumn)
            && !string.IsNullOrWhiteSpace(filterConfig.StartDate)
            && !string.IsNullOrWhiteSpace(filterConfig.EndDate))
        {
            list.Add(new FilterPredicate(
                filterConfig.DateColumn,
                FilterOperator.BetweenInclusive,
                ResolvePlaceholder(filterConfig.StartDate!, job),
                ResolvePlaceholder(filterConfig.EndDate!, job)));
        }

        return list;
    }

    private static object? ResolvePlaceholder(string raw, SyncJobConfig job)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;

        if (raw.Length > 2 && raw.StartsWith("{", StringComparison.Ordinal) && raw.EndsWith("}", StringComparison.Ordinal))
        {
            var key = raw.Trim('{', '}');
            if (job.Parameters is not null)
            {
                foreach (var kv in job.Parameters)
                {
                    if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                        return kv.Value;
                }
            }
        }

        return raw;
    }
}
