using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Exceptions;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Dependency;
using ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Schema.Services;
using ReportSyncer.Core.Sync;
using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Core.Tests.UnitTests.Sync;

public class SyncOrchestratorTests
{
    [Fact]
    public async Task RunJobAsync_LoadsConfig_ResolvesJob_ThenCallsPreFlight()
    {
        // Arrange
        var job = CreateJob("SampleJob", new TableTaskConfig("dbo.Source", "dbo.Target"));
        var config = CreateConfig(job, dryRun: true);
        var preFlightResult = CreatePreFlightResult(job.Name, dryRun: true);

        var configProvider = new Mock<IConfigurationProvider>(MockBehavior.Strict);
        var preFlightValidator = new Mock<IPreFlightValidator>(MockBehavior.Strict);

        var sequence = new MockSequence();
        configProvider.InSequence(sequence)
            .Setup(p => p.LoadAndValidateAsync("path.yaml", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        preFlightValidator.InSequence(sequence)
            .Setup(v => v.ValidateAsync(config, job, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preFlightResult);

        var orchestrator = new SyncOrchestrator(configProvider.Object, preFlightValidator.Object);

        // Act
        var result = await orchestrator.RunJobAsync("path.yaml", job.Name, null, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(JobStatus.SkippedDryRun);

        configProvider.VerifyAll();
        preFlightValidator.Verify(v => v.ValidateAsync(config, job, It.IsAny<CancellationToken>()), Times.Once);
        preFlightValidator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RunJobAsync_WhenPreFlightThrows_DoesNotProceed()
    {
        // Arrange
        var job = CreateJob("FailureJob", new TableTaskConfig("dbo.Source", "dbo.Target"));
        var config = CreateConfig(job, dryRun: false);

        var configProvider = new Mock<IConfigurationProvider>(MockBehavior.Strict);
        configProvider
            .Setup(p => p.LoadAndValidateAsync("path.yaml", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var preFlightValidator = new Mock<IPreFlightValidator>(MockBehavior.Strict);
        preFlightValidator
            .Setup(v => v.ValidateAsync(config, job, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SchemaMismatchException("boom"));

        var orchestrator = new SyncOrchestrator(configProvider.Object, preFlightValidator.Object);

        // Act
        var act = () => orchestrator.RunJobAsync("path.yaml", job.Name, null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<SchemaMismatchException>();

        configProvider.VerifyAll();
        preFlightValidator.Verify(v => v.ValidateAsync(config, job, It.IsAny<CancellationToken>()), Times.Once);
        preFlightValidator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RunJobAsync_WhenDryRun_ReturnsJobResultWithSkippedTables()
    {
        // Arrange
        var tables = new[]
        {
            new TableTaskConfig("dbo.Source1", "dbo.Target1"),
            new TableTaskConfig("dbo.Source2", "dbo.Target2")
        };
        var job = CreateJob("DryRunJob", tables);
        var config = CreateConfig(job, dryRun: true);
        var preFlightResult = CreatePreFlightResult(job.Name, dryRun: true);

        var configProvider = new Mock<IConfigurationProvider>(MockBehavior.Strict);
        configProvider
            .Setup(p => p.LoadAndValidateAsync("path.yaml", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var preFlightValidator = new Mock<IPreFlightValidator>(MockBehavior.Strict);
        preFlightValidator
            .Setup(v => v.ValidateAsync(config, job, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preFlightResult);

        var orchestrator = new SyncOrchestrator(configProvider.Object, preFlightValidator.Object);

        // Act
        var result = await orchestrator.RunJobAsync("path.yaml", job.Name, null, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.DryRun.Should().BeTrue();
        result.Status.Should().Be(JobStatus.SkippedDryRun);
        result.Tables.Should().HaveCount(tables.Length);
        result.Tables.Should().OnlyContain(t => t.Status == TableStatus.SkippedDryRun);
        result.Tables.Select(t => t.TargetTable).Should().BeEquivalentTo(tables.Select(t => t.Target));

        preFlightValidator.Verify(v => v.ValidateAsync(config, job, It.IsAny<CancellationToken>()), Times.Once);
        configProvider.VerifyAll();
        preFlightValidator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RunJobAsync_WhenNotDryRun_ThrowsSyncExecutionException()
    {
        // Arrange
        var job = CreateJob("RealRunJob", new TableTaskConfig("dbo.Source", "dbo.Target"));
        var config = CreateConfig(job, dryRun: false);
        var preFlightResult = CreatePreFlightResult(job.Name, dryRun: false);

        var configProvider = new Mock<IConfigurationProvider>(MockBehavior.Strict);
        configProvider
            .Setup(p => p.LoadAndValidateAsync("path.yaml", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var preFlightValidator = new Mock<IPreFlightValidator>(MockBehavior.Strict);
        preFlightValidator
            .Setup(v => v.ValidateAsync(config, job, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preFlightResult);

        var orchestrator = new SyncOrchestrator(configProvider.Object, preFlightValidator.Object);

        // Act
        var act = () => orchestrator.RunJobAsync("path.yaml", job.Name, null, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<SyncExecutionException>();
        ex.Which.Message.Should().Be("Execution engine not implemented. Implement Section 5.");

        configProvider.VerifyAll();
        preFlightValidator.Verify(v => v.ValidateAsync(config, job, It.IsAny<CancellationToken>()), Times.Once);
        preFlightValidator.VerifyNoOtherCalls();
    }

    private static SyncJobConfig CreateJob(string name, params TableTaskConfig[] tables)
    {
        var parameters = new Dictionary<string, string>();
        var tableTasks = tables.Length == 0 ? new[] { new TableTaskConfig("dbo.Source", "dbo.Target") } : tables;

        return new SyncJobConfig(
            name: name,
            description: $"{name} description",
            sourceConnection: "SourceConn",
            targetConnection: "TargetConn",
            parameters: parameters,
            tables: tableTasks);
    }

    private static SyncConfiguration CreateConfig(SyncJobConfig job, bool dryRun)
    {
        var run = new RunConfig(dryRun, defaultBatchSize: 1000, deleteChunkSize: 1000, useTvpIfAvailable: true);
        var safety = new SafetyConfig(forbidProdToProd: true, requireDifferentConnections: true, confirmLargeDeletePct: 0.9);
        var policy = new SchemaPolicyConfig(SchemaMismatchBehavior.Fail, requirePrimaryKey: true, allowExtraTargetColumns: false);

        var sourceConnection = new ConnectionConfig("SourceConn", "Server=.;Database=Src;Integrated Security=true;", EnvironmentType.Dev, ConnectionType.Application);
        var targetConnection = new ConnectionConfig("TargetConn", "Server=.;Database=Dst;Integrated Security=true;", EnvironmentType.Dev, ConnectionType.Reporting);

        return new SyncConfiguration(
            version: "1.0",
            run: run,
            safety: safety,
            schemaPolicy: policy,
            connections: new[] { sourceConnection, targetConnection },
            syncJobs: new[] { job });
    }

    private static PreFlightResult CreatePreFlightResult(string jobName, bool dryRun)
    {
        return new PreFlightResult(jobName, dryRun, CreateSchemaAnalysisResult());
    }

    private static SchemaAnalysisResult CreateSchemaAnalysisResult()
    {
        var sourceTableId = TableIdentifier.Parse("dbo.Source");
        var targetTableId = TableIdentifier.Parse("dbo.Target");

        var sourceTable = new TableSchema(sourceTableId, Array.Empty<ColumnSchema>(), Array.Empty<string>(), Array.Empty<ForeignKeySchema>());
        var targetTable = new TableSchema(targetTableId, Array.Empty<ColumnSchema>(), Array.Empty<string>(), Array.Empty<ForeignKeySchema>());

        var sourceSnapshot = new SchemaSnapshot(new[] { sourceTable }, SchemaRole.Source, SchemaInspectionLevel.Full);
        var targetSnapshot = new SchemaSnapshot(new[] { targetTable }, SchemaRole.Target, SchemaInspectionLevel.Full);

        var mapping = new SchemaMappingResult(true, Array.Empty<SchemaMappingError>(), new Dictionary<TableIdentifier, TableMapping>());
        var plan = new ExecutionPlan(new[] { targetTableId }, new[] { targetTableId });

        return new SchemaAnalysisResult(sourceSnapshot, targetSnapshot, mapping, plan);
    }
}
