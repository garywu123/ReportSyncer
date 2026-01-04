// ============================================================================
// File: CompositionRootSmokeTests.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-04
// Description: Smoke tests for ConsoleCompositionRoot to verify DI wiring.
// ============================================================================

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ReportSyncer.Console.Hosting;
using ReportSyncer.Core.Configuration;

namespace ReportSyncer.Console.Tests.Hosting;

/// <summary>
/// Smoke tests for <see cref="ConsoleCompositionRoot"/>.
/// These tests verify that the composition root can build a service provider
/// without executing actual database operations.
/// </summary>
public sealed class CompositionRootSmokeTests
{
    [Fact]
    public async Task BuildAsync_WithMockedConfiguration_CanResolveJobScopeFactory()
    {
        // Arrange
        var effectiveConfig = CreateMinimalConfiguration();
        var mockConfigProvider = new Mock<IConfigurationProvider>();
        
        mockConfigProvider
            .Setup(p => p.LoadAndValidateAsync(
                It.IsAny<string>(),
                It.IsAny<RuntimeOverrides>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(effectiveConfig);

        // Create a minimal service provider with mocked config provider
        var services = new ServiceCollection();
        services.AddSingleton(mockConfigProvider.Object);
        services.AddSingleton<IConfigurationLoader, YamlConfigurationLoader>();
        services.AddSingleton<DotNetToolkit.Logging.ILogService>(
            Mock.Of<DotNetToolkit.Logging.ILogService>());

        var bootstrapProvider = services.BuildServiceProvider();

        // Build final provider using the real registration logic
        var finalServices = new ServiceCollection();
        finalServices.AddSingleton(mockConfigProvider.Object);
        
        // Add required mocks for ConfigurationProvider dependencies
        services.AddSingleton(Mock.Of<DotNetToolkit.Logging.ILogService>());
        services.AddSingleton(Mock.Of<IConfigurationValidator>());
        
        finalServices.AddReportSyncerRootServices(effectiveConfig.Connections.ToList());
        finalServices.AddSingleton<JobScopeOrchestratorFactory>();

        var finalProvider = finalServices.BuildServiceProvider();

        // Act
        var factory = finalProvider.GetService<JobScopeOrchestratorFactory>();

        // Assert
        factory.Should().NotBeNull("JobScopeOrchestratorFactory should be resolvable from DI");
    }

    [Fact]
    public async Task BuildAsync_WithMockedConfiguration_CanResolveConnectionStringResolver()
    {
        // Arrange
        var effectiveConfig = CreateMinimalConfiguration();
        var services = new ServiceCollection();
        
        // Add required mocks for ConfigurationProvider dependencies
        services.AddSingleton(Mock.Of<DotNetToolkit.Logging.ILogService>());
        services.AddSingleton(Mock.Of<IConfigurationValidator>());
        
        services.AddReportSyncerRootServices(effectiveConfig.Connections.ToList());

        var provider = services.BuildServiceProvider();

        // Act
        var resolver = provider.GetService<IConnectionStringResolver>();

        // Assert
        resolver.Should().NotBeNull();
        resolver!.Get("SRC").Should().Be("Server=src;Database=AppDB;");
    }

    [Fact]
    public async Task BuildAsync_WithMockedConfiguration_CanResolveDatabaseComponentFactory()
    {
        // Arrange
        var effectiveConfig = CreateMinimalConfiguration();
        var services = new ServiceCollection();
        
        services.AddSingleton<IServiceProvider>(sp => sp);
        
        // Add required mocks for ConfigurationProvider dependencies
        services.AddSingleton(Mock.Of<DotNetToolkit.Logging.ILogService>());
        services.AddSingleton(Mock.Of<IConfigurationValidator>());
        
        services.AddReportSyncerRootServices(effectiveConfig.Connections.ToList());

        var provider = services.BuildServiceProvider();

        // Act
        var dbFactory = provider.GetService<IDatabaseComponentFactory>();

        // Assert
        dbFactory.Should().NotBeNull();
    }

    [Fact]
    public async Task BuildAsync_WithMockedConfiguration_CanResolveFactoryDelegates()
    {
        // Arrange
        var effectiveConfig = CreateMinimalConfiguration();
        var services = new ServiceCollection();
        
        services.AddSingleton<IServiceProvider>(sp => sp);
        
        // Add required mocks for ConfigurationProvider dependencies
        services.AddSingleton(Mock.Of<DotNetToolkit.Logging.ILogService>());
        services.AddSingleton(Mock.Of<IConfigurationValidator>());
        
        services.AddReportSyncerRootServices(effectiveConfig.Connections.ToList());

        var provider = services.BuildServiceProvider();

        // Act
        var funcByName = provider.GetService<Func<string, DotNetToolkit.Database.Abstractions.IDbContext>>();
        var funcByConfig = provider.GetService<Func<ConnectionConfig, DotNetToolkit.Database.Abstractions.IDbContext>>();

        // Assert
        funcByName.Should().NotBeNull("Func<string, IDbContext> should be registered");
        funcByConfig.Should().NotBeNull("Func<ConnectionConfig, IDbContext> should be registered");
    }

    [Fact]
    public async Task BuildAsync_WithMockedConfiguration_CoreServicesAreRegistered()
    {
        // Arrange
        var effectiveConfig = CreateMinimalConfiguration();
        var services = new ServiceCollection();
        
        services.AddSingleton<IServiceProvider>(sp => sp);
        
        // Add required mocks for ConfigurationProvider dependencies
        services.AddSingleton(Mock.Of<DotNetToolkit.Logging.ILogService>());
        services.AddSingleton(Mock.Of<IConfigurationValidator>());
        
        services.AddReportSyncerRootServices(effectiveConfig.Connections.ToList());

        var provider = services.BuildServiceProvider();

        // Act & Assert - verify Core services can be resolved
        provider.GetService<IConfigurationProvider>().Should().NotBeNull();
        provider.GetService<IConfigurationLoader>().Should().NotBeNull();
        // Note: IConfigurationValidator is internal, not directly resolvable
        provider.GetService<Core.Schema.ISchemaInspector>().Should().NotBeNull();
        provider.GetService<Core.Schema.Mapping.ISchemaMapper>().Should().NotBeNull();
        provider.GetService<Core.Schema.Dependency.IDependencyResolver>().Should().NotBeNull();
        provider.GetService<Core.Schema.Services.ISchemaService>().Should().NotBeNull();
        provider.GetService<Core.Sync.Sql.ISqlQueryBuilder>().Should().NotBeNull();
        provider.GetService<Core.Sync.IDataWriter>().Should().NotBeNull();
        provider.GetService<Core.Sync.IPreFlightValidator>().Should().NotBeNull();
        provider.GetService<Core.Observability.IJobProgressReporter>().Should().NotBeNull();
    }

    private static SyncConfiguration CreateMinimalConfiguration()
    {
        var connections = new[]
        {
            new ConnectionConfig("SRC", "Server=src;Database=AppDB;", EnvironmentType.Dev, ConnectionType.Application),
            new ConnectionConfig("TGT", "Server=tgt;Database=AppDB;", EnvironmentType.Dev, ConnectionType.Reporting)
        };

        var jobs = new[]
        {
            new SyncJobConfig(
                name: "TestJob",
                description: null,
                sourceConnection: "SRC",
                targetConnection: "TGT",
                parameters: new Dictionary<string, string>(),
                tables: new[] { new TableTaskConfig("dbo.Test", "dbo.Test", true) })
        };

        return new SyncConfiguration(
            version: "1.0",
            run: new RunConfig(
                dryRun: false,
                defaultBatchSize: 1000,
                deleteChunkSize: 500,
                useTvpIfAvailable: false),
            safety: new SafetyConfig(
                forbidProdToProd: true,
                requireDifferentConnections: true,
                confirmLargeDeletePct: 0.5),
            schemaPolicy: new SchemaPolicyConfig(
                onMismatch: SchemaMismatchBehavior.Fail,
                requirePrimaryKey: true,
                allowExtraTargetColumns: false),
            connections: connections,
            syncJobs: jobs);
    }
}
