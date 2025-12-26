using DotNetToolkit.General;
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

public class PreFlightValidatorTests
{
    [Fact]
    public async Task ValidateAsync_WhenSchemaServiceFails_ThrowsSchemaMismatchExceptionWithDetails()
    {
        // Arrange
        var job = CreateJob();
        var config = CreateConfig(job, dryRun: false);
        var reason = "Missing dependency: dbo.Parent";

        var schemaService = new Mock<ISchemaService>(MockBehavior.Strict);
        schemaService
            .Setup(s => s.AnalyzeJobAsync(config, job, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SchemaAnalysisResult>.Fail(reason));

        var log = new Mock<DotNetToolkit.Logging.ILogService>(MockBehavior.Strict);
        log.Setup(l => l.LogError(It.IsAny<string>(), It.IsAny<Exception?>())).Verifiable();

        var validator = new PreFlightValidator(schemaService.Object, log.Object);

        // Act
        var act = () => validator.ValidateAsync(config, job, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<SchemaMismatchException>();
        ex.Which.Message.Should().Contain(job.Name).And.Contain(reason);
        schemaService.VerifyAll();
        log.Verify(l => l.LogError(It.Is<string>(m => m.Contains(job.Name) && m.Contains(reason)), It.IsAny<Exception?>()), Times.Once);
        log.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ValidateAsync_WhenSchemaServiceSucceeds_ReturnsPreFlightResultWithDryRun()
    {
        // Arrange
        var job = CreateJob();
        var config = CreateConfig(job, dryRun: true);
        var analysis = CreateSchemaAnalysisResult();

        var schemaService = new Mock<ISchemaService>(MockBehavior.Strict);
        schemaService
            .Setup(s => s.AnalyzeJobAsync(config, job, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SchemaAnalysisResult>.Ok(analysis));

        var log = new Mock<DotNetToolkit.Logging.ILogService>(MockBehavior.Strict);
        log.Setup(l => l.LogInformation(It.IsAny<string>())).Verifiable();

        var validator = new PreFlightValidator(schemaService.Object, log.Object);

        // Act
        var result = await validator.ValidateAsync(config, job, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.JobName.Should().Be(job.Name);
        result.DryRun.Should().BeTrue();
        result.Schema.ExecutionPlan.Should().NotBeNull();

        schemaService.VerifyAll();
        log.Verify(l => l.LogInformation(It.Is<string>(m => m.Contains(job.Name))), Times.Once);
        log.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ValidateAsync_InvokesSchemaServiceOnce()
    {
        // Arrange
        var job = CreateJob();
        var config = CreateConfig(job, dryRun: false);
        var analysis = CreateSchemaAnalysisResult();

        var schemaService = new Mock<ISchemaService>(MockBehavior.Strict);
        schemaService
            .Setup(s => s.AnalyzeJobAsync(config, job, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SchemaAnalysisResult>.Ok(analysis));

        var log = new Mock<DotNetToolkit.Logging.ILogService>(MockBehavior.Strict);
        log.Setup(l => l.LogInformation(It.IsAny<string>())).Verifiable();

        var validator = new PreFlightValidator(schemaService.Object, log.Object);

        // Act
        await validator.ValidateAsync(config, job, CancellationToken.None);

        // Assert
        schemaService.Verify(s => s.AnalyzeJobAsync(config, job, It.IsAny<CancellationToken>()), Times.Once);
        log.Verify(l => l.LogInformation(It.IsAny<string>()), Times.Once);
        log.VerifyNoOtherCalls();
    }

    private static SyncJobConfig CreateJob()
    {
        var table = new TableTaskConfig("dbo.Source", "dbo.Target");
        return new SyncJobConfig(
            name: "SampleJob",
            description: "desc",
            sourceConnection: "SourceConn",
            targetConnection: "TargetConn",
            parameters: new Dictionary<string, string>(),
            tables: new[] { table });
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
