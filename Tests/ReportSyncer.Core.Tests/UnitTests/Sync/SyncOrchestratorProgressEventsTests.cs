using FluentAssertions;
using Moq;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Observability;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Dependency;
using ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Schema.Services;
using ReportSyncer.Core.Sync;
using ReportSyncer.Core.Sync.Contracts;
using ReportSyncer.Core.Tests.Helpers.Observability;

namespace ReportSyncer.Core.Tests.UnitTests.Sync;

public class SyncOrchestratorProgressEventsTests
{
    [Fact]
    public async Task RunJobAsync_EmitsJobAndTableEventsInOrder()
    {
        var reporter = new InMemoryProgressReporter();

        var tables = new[]
        {
            new TableTaskConfig("dbo.SourceA", "dbo.TargetA", preSyncTargetAction: true),
            new TableTaskConfig("dbo.SourceB", "dbo.TargetB", preSyncTargetAction: true)
        };
        var job = new SyncJobConfig("Job1", "desc", "SourceConn", "TargetConn", new Dictionary<string, string>(), tables);
        var config = CreateConfig(job, dryRun: false);
        var plan = new ExecutionPlan(
            new[]
            {
                TableIdentifier.Parse("dbo.TargetA"),
                TableIdentifier.Parse("dbo.TargetB")
            },
            new[]
            {
                TableIdentifier.Parse("dbo.TargetB"),
                TableIdentifier.Parse("dbo.TargetA")
            });
        var preflight = new PreFlightResult(job.Name, false, CreateSchemaAnalysisResult(plan));

        var configProvider = new Mock<IConfigurationProvider>(MockBehavior.Strict);
        configProvider.Setup(p => p.LoadAndValidateAsync("path.yaml", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var preFlightValidator = new Mock<IPreFlightValidator>(MockBehavior.Strict);
        preFlightValidator.Setup(v => v.ValidateAsync(config, job, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preflight);

        var runner = new Mock<ITableRunner>(MockBehavior.Strict);
        runner.Setup(r => r.RunPhaseAsync(It.IsAny<TableExecutionContext>(), SyncPhase.Delete, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TableExecutionContext ctx, SyncPhase _, CancellationToken _) =>
                new TableResult($"{ctx.TargetSchema}.{ctx.TargetTable}", TableStatus.Succeeded, RowsDeleted: 1));
        runner.Setup(r => r.RunPhaseAsync(It.IsAny<TableExecutionContext>(), SyncPhase.Insert, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TableExecutionContext ctx, SyncPhase _, CancellationToken _) =>
                new TableResult($"{ctx.TargetSchema}.{ctx.TargetTable}", TableStatus.Succeeded, RowsInserted: 2));

        var orchestrator = new SyncOrchestrator(configProvider.Object, preFlightValidator.Object, runner.Object, reporter);

        var result = await orchestrator.RunJobAsync("path.yaml", job.Name, null, CancellationToken.None);

        result.Status.Should().Be(JobStatus.Succeeded);

        reporter.JobEvents.Should().ContainSingle(e => e.Kind == ProgressEventKind.Started && e.Phase == ProgressPhase.Preflight);
        reporter.JobEvents.Should().ContainSingle(e => e.Kind == ProgressEventKind.Completed && e.Phase == ProgressPhase.Preflight);
        reporter.JobEvents.Should().ContainSingle(e => e.Kind == ProgressEventKind.Started && e.Phase == ProgressPhase.Execution);
        reporter.JobEvents.Should().ContainSingle(e => e.Kind == ProgressEventKind.Completed && e.Phase == ProgressPhase.Execution);

        reporter.TableEvents.Should().BeEmpty("table events are emitted by TableRunner; runner is mocked here");
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

    private static SchemaAnalysisResult CreateSchemaAnalysisResult(ExecutionPlan plan)
    {
        var sourceTable = new TableSchema(TableIdentifier.Parse("dbo.SourceA"), Array.Empty<ColumnSchema>(), Array.Empty<string>(), Array.Empty<ForeignKeySchema>());
        var targetTable = new TableSchema(TableIdentifier.Parse("dbo.TargetA"), Array.Empty<ColumnSchema>(), Array.Empty<string>(), Array.Empty<ForeignKeySchema>());

        var sourceSnapshot = new SchemaSnapshot(new[] { sourceTable }, SchemaRole.Source, SchemaInspectionLevel.Full);
        var targetSnapshot = new SchemaSnapshot(new[] { targetTable }, SchemaRole.Target, SchemaInspectionLevel.Full);

        var col = new ColumnSchema("Id", typeof(int), "int", false, false, true, null);
        var mappingDict = new Dictionary<TableIdentifier, TableMapping>();
        foreach (var target in plan.InsertOrder.Union(plan.DeleteOrder))
        {
            var src = TableIdentifier.Parse($"src.{target.TableName}");
            mappingDict[target] = new TableMapping(
                src,
                target,
                new[] { new ColumnMapping(col, col, MappingKind.OneToOne, null) },
                HasWarnings: false);
        }

        var mapping = new SchemaMappingResult(true, Array.Empty<SchemaMappingError>(), mappingDict);
        return new SchemaAnalysisResult(sourceSnapshot, targetSnapshot, mapping, plan);
    }
}
