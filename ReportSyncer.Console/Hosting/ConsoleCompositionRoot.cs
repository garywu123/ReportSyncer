// ============================================================================
// File: ConsoleCompositionRoot.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-04
// Description: Two-phase composition root for Console Host DI wiring.
// ============================================================================

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReportSyncer.Console.Logging;
using ReportSyncer.Core.Configuration;
using Serilog;

namespace ReportSyncer.Console.Hosting;

/// <summary>
/// Builds the Console Host's dependency injection container in two phases:
/// 1. Bootstrap phase: minimal services for configuration loading (uses FallbackLogger)
/// 2. Final phase: complete service graph including connection-dependent services (uses Serilog)
/// </summary>
/// <remarks>
/// The two-phase build is necessary because:
/// - We need IConfigurationProvider to load the YAML and get connection definitions
/// - Connection definitions are required to register IConnectionStringResolver and factory delegates
/// - These factory delegates are dependencies of Core services like SqlDataWriter
/// 
/// This approach allows the effective configuration to be loaded once and used to properly
/// wire all connection-dependent services.
/// 
/// The Serilog logger must be created and configured in Program.cs before calling BuildAsync,
/// as it requires settings from appsettings.json that are loaded at startup.
/// </remarks>
public sealed class ConsoleCompositionRoot
{
    /// <summary>
    /// Builds a complete service provider with loaded configuration.
    /// </summary>
    /// <param name="configPath">Path to the YAML configuration file.</param>
    /// <param name="serilogLogger">The configured Serilog logger from Program.cs.</param>
    /// <param name="overrides">Optional runtime overrides to apply during configuration loading.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="ConsoleHost"/> instance containing the service provider and effective configuration.
    /// </returns>
    /// <exception cref="ConfigurationException">
    /// Thrown when configuration loading or validation fails.
    /// </exception>
    public async Task<ConsoleHost> BuildAsync(
        string configPath,
        Serilog.ILogger serilogLogger,
        RuntimeOverrides? overrides,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);
        ArgumentNullException.ThrowIfNull(serilogLogger);

        // Phase 1: Bootstrap provider for configuration loading (uses FallbackLogger)
        using var fallbackLogger = new FallbackLogger();
        fallbackLogger.Information("Starting configuration loading (bootstrap phase)");

        var bootstrapProvider = BuildBootstrapProvider(fallbackLogger);

        // Load effective configuration using bootstrap provider
        var configProvider = bootstrapProvider.GetRequiredService<IConfigurationProvider>();
        var effectiveConfig = await configProvider.LoadAndValidateAsync(configPath, overrides, ct);

        fallbackLogger.Information($"Configuration loaded successfully: {effectiveConfig.SyncJobs.Count} job(s)");

        // Phase 2: Final provider with connection-dependent services (uses Serilog)
        var finalProvider = BuildFinalProvider(effectiveConfig, serilogLogger);

        return new ConsoleHost(finalProvider, effectiveConfig, configPath, overrides);
    }

    /// <summary>
    /// Phase 1: Builds a minimal service provider for configuration loading.
    /// </summary>
    /// <param name="fallbackLogger">Simple logger for bootstrap phase diagnostics.</param>
    /// <remarks>
    /// Bootstrap provider includes:
    /// - FallbackLoggerProvider (bridges MEL ILogger to FallbackLogger)
    /// - Configuration loading and validation services
    /// 
    /// This allows ConfigurationProvider and YamlConfigurationLoader (which depend on ILogger)
    /// to log through FallbackLogger during the bootstrap phase.
    /// </remarks>
    private ServiceProvider BuildBootstrapProvider(FallbackLogger fallbackLogger)
    {
        var services = new ServiceCollection();

        // Register FallbackLogger as the logging provider for bootstrap phase
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(new FallbackLoggerProvider(fallbackLogger));
        });

        // Register configuration loading services
        services.AddSingleton<IConfigurationLoader, YamlConfigurationLoader>();
        services.AddSingleton<IConfigurationProvider, ConfigurationProvider>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Phase 2: Builds the final service provider with all services including connection-dependent ones.
    /// </summary>
    /// <param name="effectiveConfig">The loaded effective configuration.</param>
    /// <param name="serilogLogger">The configured Serilog logger from Program.cs.</param>
    /// <remarks>
    /// This method registers the provided Serilog logger (which was configured with RingBuffer, File, 
    /// and Console sinks in Program.cs) as the Microsoft.Extensions.Logging provider.
    /// </remarks>
    private ServiceProvider BuildFinalProvider(
        SyncConfiguration effectiveConfig,
        Serilog.ILogger serilogLogger)
    {
        var services = new ServiceCollection();

        // Register the already-configured Serilog logger from Program.cs
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(serilogLogger, dispose: false);
        });

        // Register Core services with connection bindings
        services.AddReportSyncerRootServices(effectiveConfig.Connections.ToList());

        // Register JobScopeOrchestratorFactory
        services.AddSingleton<JobScopeOrchestratorFactory>();

        // UI components will be created and wired in Program.cs
        // Reporter registration will be handled there to avoid tight coupling

        return services.BuildServiceProvider();
    }
}

/// <summary>
/// Wrapper for the configured service provider and effective configuration.
/// </summary>
/// <remarks>
/// This class represents the fully initialized Console Host with all dependencies wired.
/// It provides methods to run jobs using the configured services.
/// </remarks>
public sealed class ConsoleHost
{
    private readonly IServiceProvider _services;
    private readonly SyncConfiguration _effectiveConfig;
    private readonly string _configPath;
    private readonly RuntimeOverrides? _overrides;

    internal ConsoleHost(
        IServiceProvider services,
        SyncConfiguration effectiveConfig,
        string configPath,
        RuntimeOverrides? overrides)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _effectiveConfig = effectiveConfig ?? throw new ArgumentNullException(nameof(effectiveConfig));
        _configPath = configPath ?? throw new ArgumentNullException(nameof(configPath));
        _overrides = overrides;
    }

    /// <summary>
    /// Gets the service provider containing all registered services.
    /// </summary>
    public IServiceProvider Services => _services;

    /// <summary>
    /// Gets the effective configuration loaded at startup.
    /// </summary>
    public SyncConfiguration EffectiveConfig => _effectiveConfig;

    /// <summary>
    /// Runs a single sync job by name.
    /// </summary>
    /// <param name="jobName">The name of the job to run.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The job result.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the job name is not found in configuration.
    /// </exception>
    public async Task<Core.Sync.Contracts.JobResult> RunJobAsync(string jobName, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);

        var factory = _services.GetRequiredService<JobScopeOrchestratorFactory>();
        var orchestrator = factory.Create(_configPath, _effectiveConfig, jobName, _overrides);

        return await orchestrator.RunJobAsync(_configPath, jobName, _overrides, ct);
    }

    /// <summary>
    /// Runs all sync jobs defined in the configuration sequentially.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A collection of job results.</returns>
    public async Task<IReadOnlyList<Core.Sync.Contracts.JobResult>> RunAllJobsAsync(CancellationToken ct)
    {
        var results = new List<Core.Sync.Contracts.JobResult>();

        foreach (var job in _effectiveConfig.SyncJobs)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            var result = await RunJobAsync(job.Name, ct);
            results.Add(result);
        }

        return results;
    }
}
