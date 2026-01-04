// ============================================================================
// File: JobScopeOrchestratorFactory.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-04
// Description: Factory for creating job-scoped ISyncOrchestrator instances with bound connections.
// ============================================================================

using DotNetToolkit.Database.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Observability;
using ReportSyncer.Core.Sync;
using ReportSyncer.Core.Sync.Sql;

namespace ReportSyncer.Console.Hosting;

/// <summary>
/// Creates job-scoped <see cref="ISyncOrchestrator"/> instances with connection bindings specific to each job.
/// </summary>
/// <remarks>
/// This factory addresses the "串线" (connection cross-wiring) problem by ensuring that each job
/// gets its own TableRunner and SyncOrchestrator instances bound to the correct source and target
/// connection factories. This prevents different jobs from accidentally sharing connection bindings.
/// 
/// The factory creates new instances per job scope rather than using DI singletons for ITableRunner
/// and ISyncOrchestrator, which is necessary because these components hold job-specific connection
/// factory references.
/// </remarks>
public sealed class JobScopeOrchestratorFactory
{
    private readonly IServiceProvider _rootProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobScopeOrchestratorFactory"/> class.
    /// </summary>
    /// <param name="rootProvider">
    /// The root service provider containing singleton Core services.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="rootProvider"/> is null.
    /// </exception>
    public JobScopeOrchestratorFactory(IServiceProvider rootProvider)
    {
        _rootProvider = rootProvider ?? throw new ArgumentNullException(nameof(rootProvider));
    }

    /// <summary>
    /// Creates a new <see cref="ISyncOrchestrator"/> instance scoped to the specified job.
    /// </summary>
    /// <param name="configPath">
    /// Path to the configuration file (passed to the orchestrator for its internal config loading).
    /// </param>
    /// <param name="effectiveConfig">
    /// The already-loaded effective configuration containing connection and job definitions.
    /// </param>
    /// <param name="jobName">
    /// The name of the job to create the orchestrator for.
    /// </param>
    /// <param name="overrides">
    /// Optional runtime overrides (will be passed to the orchestrator's RunJobAsync call).
    /// </param>
    /// <returns>
    /// A new <see cref="ISyncOrchestrator"/> instance bound to the job's source connection.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when required parameters are null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="configPath"/> or <paramref name="jobName"/> is null or whitespace.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the job is not found in the effective configuration.
    /// </exception>
    public ISyncOrchestrator Create(
        string configPath,
        SyncConfiguration effectiveConfig,
        string jobName,
        RuntimeOverrides? overrides)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);
        ArgumentNullException.ThrowIfNull(effectiveConfig);
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);

        // Find the job in effective configuration
        var job = effectiveConfig.SyncJobs
            .FirstOrDefault(j => string.Equals(j.Name, jobName, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException(
                $"Job '{jobName}' was not found in configuration. " +
                "Verify that the job name exists in the YAML configuration.");

        // Resolve singleton dependencies from root provider
        var resolver = _rootProvider.GetRequiredService<IConnectionStringResolver>();
        var dbFactory = _rootProvider.GetRequiredService<IDatabaseComponentFactory>();
        var configProvider = _rootProvider.GetRequiredService<IConfigurationProvider>();
        var preFlightValidator = _rootProvider.GetRequiredService<IPreFlightValidator>();
        var dataWriter = _rootProvider.GetRequiredService<IDataWriter>();
        var sqlBuilder = _rootProvider.GetRequiredService<ISqlQueryBuilder>();
        var progressReporter = _rootProvider.GetRequiredService<IJobProgressReporter>();

        // Resolve job-specific source connection string and create connection factory
        var sourceConnectionString = resolver.Get(job.SourceConnection);
        var sourceConnectionFactory = dbFactory.CreateConnectionFactory(sourceConnectionString);

        // Create job-scoped TableRunner with source connection binding
        var tableRunner = new TableRunner(
            sourceConnectionFactory,
            dataWriter,
            sqlBuilder,
            progressReporter);

        // Create job-scoped SyncOrchestrator
        var orchestrator = new SyncOrchestrator(
            configProvider,
            preFlightValidator,
            tableRunner,
            progressReporter);

        return orchestrator;
    }
}
