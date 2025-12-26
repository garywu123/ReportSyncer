// ============================================================================
// File: SyncOrchestrator.cs
// Author: Codex
// Project: ReportSyncer
// Date: 2025-12-26
// Description: Orchestrates sync job execution with preflight and dry-run flow.
// ============================================================================

using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Exceptions;
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
    /// <summary>
    /// Initializes a new instance of the <see cref="SyncOrchestrator"/> class.
    /// </summary>
    /// <param name="configurationProvider">Provides configuration loading and validation.</param>
    /// <param name="preFlightValidator">Validates jobs before execution.</param>
    public SyncOrchestrator(
        IConfigurationProvider configurationProvider,
        IPreFlightValidator preFlightValidator)
    {
        _configurationProvider = configurationProvider ?? throw new ArgumentNullException(nameof(configurationProvider));
        _preFlightValidator = preFlightValidator ?? throw new ArgumentNullException(nameof(preFlightValidator));
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

        var effectiveConfig = await _configurationProvider
            .LoadAndValidateAsync(configPath, overrides, ct)
            .ConfigureAwait(false);

        var job = effectiveConfig.SyncJobs.FirstOrDefault(j =>
            string.Equals(j.Name, jobName, StringComparison.OrdinalIgnoreCase)) ?? throw new ConfigurationException(
                string.Format(CultureInfo.InvariantCulture, "Job '{0}' was not found in configuration.", jobName));

        var preFlight = await _preFlightValidator
            .ValidateAsync(effectiveConfig, job, ct)
            .ConfigureAwait(false);

        var dryRun = preFlight.DryRun;
        if (dryRun)
        {
            var tableResults = job.Tables
                .Select(t => new TableResult(t.Target, TableStatus.SkippedDryRun))
                .ToArray();

            return new JobResult(
                job.Name,
                JobStatus.SkippedDryRun,
                dryRun: true,
                startedAtUtc: startedAt,
                finishedAtUtc: DateTimeOffset.UtcNow,
                tables: tableResults);
        }

        throw new SyncExecutionException("Execution engine not implemented. Implement Section 5.");
    }
}
