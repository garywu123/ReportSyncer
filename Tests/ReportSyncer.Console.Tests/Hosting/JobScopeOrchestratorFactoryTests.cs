// ============================================================================
// File: JobScopeOrchestratorFactoryTests.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-04
// Description: Unit tests for JobScopeOrchestratorFactory.
// ============================================================================

using DotNetToolkit.Database.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ReportSyncer.Console.Hosting;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Observability;
using ReportSyncer.Core.Sync;
using ReportSyncer.Core.Sync.Sql;

namespace ReportSyncer.Console.Tests.Hosting;

/// <summary>
/// Tests for <see cref="JobScopeOrchestratorFactory"/>.
/// </summary>
public sealed class JobScopeOrchestratorFactoryTests
{
    [Fact]
    public void Create_ForJobA_UsesJobASourceConnection()
    {
        // Arrange
        var effectiveConfig = CreateTestConfiguration();
        var mockResolver = new Mock<IConnectionStringResolver>();
        var mockDbFactory = new Mock<IDatabaseComponentFactory>();
        var mockConnectionFactory = new Mock<IDbConnectionFactory>();

        mockResolver.Setup(r => r.Get("SRC_A")).Returns("ConnStringA");
        mockDbFactory.Setup(f => f.CreateConnectionFactory("ConnStringA"))
            .Returns(mockConnectionFactory.Object);

        var provider = CreateTestServiceProvider(mockResolver.Object, mockDbFactory.Object);
        var factory = new JobScopeOrchestratorFactory(provider);

        // Act
        var orchestrator = factory.Create("config.yaml", effectiveConfig, "JobA", null);

        // Assert
        orchestrator.Should().NotBeNull();
        mockResolver.Verify(r => r.Get("SRC_A"), Times.Once);
        mockDbFactory.Verify(f => f.CreateConnectionFactory("ConnStringA"), Times.Once);
    }

    [Fact]
    public void Create_ForJobB_UsesJobBSourceConnection()
    {
        // Arrange
        var effectiveConfig = CreateTestConfiguration();
        var mockResolver = new Mock<IConnectionStringResolver>();
        var mockDbFactory = new Mock<IDatabaseComponentFactory>();
        var mockConnectionFactory = new Mock<IDbConnectionFactory>();

        mockResolver.Setup(r => r.Get("SRC_B")).Returns("ConnStringB");
        mockDbFactory.Setup(f => f.CreateConnectionFactory("ConnStringB"))
            .Returns(mockConnectionFactory.Object);

        var provider = CreateTestServiceProvider(mockResolver.Object, mockDbFactory.Object);
        var factory = new JobScopeOrchestratorFactory(provider);

        // Act
        var orchestrator = factory.Create("config.yaml", effectiveConfig, "JobB", null);

        // Assert
        orchestrator.Should().NotBeNull();
        mockResolver.Verify(r => r.Get("SRC_B"), Times.Once);
        mockDbFactory.Verify(f => f.CreateConnectionFactory("ConnStringB"), Times.Once);
        // Verify that ConnStringA was NOT requested
        mockResolver.Verify(r => r.Get("SRC_A"), Times.Never);
    }

    [Fact]
    public void Create_WithUnknownJobName_ThrowsInvalidOperationException()
    {
        // Arrange
        var effectiveConfig = CreateTestConfiguration();
        var mockResolver = new Mock<IConnectionStringResolver>();
        var mockDbFactory = new Mock<IDatabaseComponentFactory>();

        var provider = CreateTestServiceProvider(mockResolver.Object, mockDbFactory.Object);
        var factory = new JobScopeOrchestratorFactory(provider);

        // Act
        var act = () => factory.Create("config.yaml", effectiveConfig, "UnknownJob", null);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*UnknownJob*")
            .WithMessage("*not found*");
    }

    [Fact]
    public void Create_WithNullConfigPath_ThrowsArgumentException()
    {
        // Arrange
        var effectiveConfig = CreateTestConfiguration();
        var provider = CreateTestServiceProvider();
        var factory = new JobScopeOrchestratorFactory(provider);

        // Act
        var act = () => factory.Create(null!, effectiveConfig, "JobA", null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNullEffectiveConfig_ThrowsArgumentNullException()
    {
        // Arrange
        var provider = CreateTestServiceProvider();
        var factory = new JobScopeOrchestratorFactory(provider);

        // Act
        var act = () => factory.Create("config.yaml", null!, "JobA", null);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNullJobName_ThrowsArgumentException()
    {
        // Arrange
        var effectiveConfig = CreateTestConfiguration();
        var provider = CreateTestServiceProvider();
        var factory = new JobScopeOrchestratorFactory(provider);

        // Act
        var act = () => factory.Create("config.yaml", effectiveConfig, null!, null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    private static SyncConfiguration CreateTestConfiguration()
    {
        var connections = new[]
        {
            new ConnectionConfig("SRC_A", "ConnA", EnvironmentType.Dev, ConnectionType.Application),
            new ConnectionConfig("TGT_A", "ConnA_Tgt", EnvironmentType.Dev, ConnectionType.Reporting),
            new ConnectionConfig("SRC_B", "ConnB", EnvironmentType.Dev, ConnectionType.Application),
            new ConnectionConfig("TGT_B", "ConnB_Tgt", EnvironmentType.Dev, ConnectionType.Reporting)
        };

        var jobs = new[]
        {
            new SyncJobConfig(
                name: "JobA",
                description: null,
                sourceConnection: "SRC_A",
                targetConnection: "TGT_A",
                parameters: new Dictionary<string, string>(),
                tables: new[] { new TableTaskConfig("dbo.TableA", "dbo.TableA", true) }),
            new SyncJobConfig(
                name: "JobB",
                description: null,
                sourceConnection: "SRC_B",
                targetConnection: "TGT_B",
                parameters: new Dictionary<string, string>(),
                tables: new[] { new TableTaskConfig("dbo.TableB", "dbo.TableB", true) })
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

    private static IServiceProvider CreateTestServiceProvider(
        IConnectionStringResolver? resolver = null,
        IDatabaseComponentFactory? dbFactory = null)
    {
        var services = new ServiceCollection();

        // Register mocks or defaults
        services.AddSingleton(resolver ?? Mock.Of<IConnectionStringResolver>());
        services.AddSingleton(dbFactory ?? Mock.Of<IDatabaseComponentFactory>());
        services.AddSingleton(Mock.Of<IConfigurationProvider>());
        services.AddSingleton(Mock.Of<IPreFlightValidator>());
        services.AddSingleton(Mock.Of<IDataWriter>());
        services.AddSingleton(Mock.Of<ISqlQueryBuilder>());
        services.AddSingleton(Mock.Of<IJobProgressReporter>());

        return services.BuildServiceProvider();
    }
}
