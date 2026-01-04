// ============================================================================
// File: ServiceRegistration.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-04
// Description: Extension methods for registering ReportSyncer Core services in DI container.
// ============================================================================

using DotNetToolkit.Database.Abstractions;
using DotNetToolkit.Logging;
using DotNetToolkit.Logging.Services;
using Microsoft.Extensions.DependencyInjection;
using ILogService = DotNetToolkit.Logging.ILogService;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Observability;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Dependency;
using ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Schema.Services;
using ReportSyncer.Core.Sync;
using ReportSyncer.Core.Sync.Sql;

namespace ReportSyncer.Console.Hosting;

/// <summary>
/// Provides extension methods for registering ReportSyncer Core services in the DI container.
/// </summary>
/// <remarks>
/// This class encapsulates the wiring logic for Core services at the root level.
/// It registers shared singletons and factory delegates required by Core components.
/// 
/// CRITICAL: ITableRunner and ISyncOrchestrator are intentionally NOT registered here
/// because they require job-scope connection bindings. Use JobScopeOrchestratorFactory
/// to create job-specific orchestrator instances.
/// </remarks>
public static class ServiceRegistration
{
    /// <summary>
    /// Registers ReportSyncer Core services and factory delegates.
    /// </summary>
    /// <param name="services">The service collection to register services into.</param>
    /// <param name="connections">The connection configurations from the effective configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="connections"/> is null.
    /// </exception>
    public static IServiceCollection AddReportSyncerRootServices(
        this IServiceCollection services,
        IReadOnlyList<ConnectionConfig> connections)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(connections);

        // --- Connection and Database Infrastructure ---
        services.AddSingleton<IConnectionStringResolver>(sp =>
            new ConnectionStringResolver(connections));

        services.AddSingleton<IDatabaseComponentFactory, SqlServerDatabaseComponentFactory>();

        // Register factory delegates required by Core components
        RegisterDatabaseFactoryDelegates(services);

        // --- Logging ---
        // Note: Actual Serilog configuration is done by Console Host at startup
        // Only register if not already registered (allows tests to provide mocks)
        if (!services.Any(sd => sd.ServiceType == typeof(ILogService)))
        {
            services.AddSingleton<ILogService, SerilogLogService>();
        }

        // --- Configuration Layer ---
        services.AddSingleton<IConfigurationLoader, YamlConfigurationLoader>();
        // ConfigurationValidator is internal, so we register ConfigurationProvider which creates it internally
        services.AddSingleton<IConfigurationProvider, ConfigurationProvider>();

        // --- Schema Layer ---
        services.AddSingleton<ISchemaInspector, SqlServerSchemaInspector>();
        services.AddSingleton<ISchemaMapper, SchemaMapper>();
        services.AddSingleton<IDependencyResolver, DependencyResolver>();
        services.AddSingleton<ISchemaService, SchemaService>();

        // --- Sync Layer ---
        services.AddSingleton<ISqlQueryBuilder, SqlServerQueryBuilder>();
        services.AddSingleton<IdentityInsertManager>();
        services.AddSingleton<IDataWriter, SqlDataWriter>();
        services.AddSingleton<IPreFlightValidator, PreFlightValidator>();

        // --- Observability ---
        // IJobProgressReporter should be registered by Console Host
        // If not already registered, provide a null implementation
        if (!services.Any(sd => sd.ServiceType == typeof(IJobProgressReporter)))
        {
            services.AddSingleton<IJobProgressReporter>(sp => NullJobProgressReporter.Instance);
        }

        return services;
    }

    /// <summary>
    /// Registers factory delegate functions required by Core components.
    /// </summary>
    private static void RegisterDatabaseFactoryDelegates(IServiceCollection services)
    {
        // Factory delegate: Func<string, IDbContext> (by connection name)
        // Used by: SqlDataWriter, IdentityInsertManager, SqlServerPermissionProfiler
        services.AddSingleton<Func<string, IDbContext>>(sp =>
        {
            var resolver = sp.GetRequiredService<IConnectionStringResolver>();
            var factory = sp.GetRequiredService<IDatabaseComponentFactory>();

            return (string connectionName) =>
            {
                var connectionString = resolver.Get(connectionName);
                return factory.CreateDbContext(connectionString);
            };
        });

        // Factory delegate: Func<ConnectionConfig, IDbContext> (by ConnectionConfig)
        // Used by: Some Core components that receive ConnectionConfig directly
        services.AddSingleton<Func<ConnectionConfig, IDbContext>>(sp =>
        {
            var factory = sp.GetRequiredService<IDatabaseComponentFactory>();

            return (ConnectionConfig cfg) =>
            {
                ArgumentNullException.ThrowIfNull(cfg);
                return factory.CreateDbContext(cfg.ConnectionString);
            };
        });
    }

    /// <summary>
    /// Null implementation of IJobProgressReporter for scenarios where progress reporting is not needed.
    /// </summary>
    private sealed class NullJobProgressReporter : IJobProgressReporter
    {
        public static readonly NullJobProgressReporter Instance = new();
        
        private NullJobProgressReporter() { }

        public void Report(JobProgressEvent evt) { }
        public void Report(TableProgressEvent evt) { }
    }
}
