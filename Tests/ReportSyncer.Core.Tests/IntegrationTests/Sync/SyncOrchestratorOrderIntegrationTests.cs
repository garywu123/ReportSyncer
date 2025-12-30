using FluentAssertions;
using Moq;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Dependency;
using ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Schema.Services;
using ReportSyncer.Core.Sync;
using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Core.Tests.IntegrationTests.Sync;

public class SyncOrchestratorOrderIntegrationTests
{
    [Fact]
    public async Task RealRun_RespectsExecutionPlanOrder()
    {
        // Arrange: build a job with three tables and an execution plan.
        var tables = new[]
        {
            new TableTaskConfig("dbo.SourceA", "dbo.TargetA", preSyncTargetAction: true),
            new TableTaskConfig("dbo.SourceB", "dbo.TargetB", preSyncTargetAction: true),
            new TableTaskConfig("dbo.SourceC", "dbo.TargetC", preSyncTargetAction: true)
        };
        var job = new SyncJobConfig(
            "JobOrder",
            "desc",
            "SourceConn",
            "TargetConn",
            new Dictionary<string, string>(),
            tables);

        var run = new RunConfig(dryRun: false, defaultBatchSize: 1000, deleteChunkSize: 1000, useTvpIfAvailable: true);
        var config = new SyncConfiguration(
            "1.0",
            run,
            new SafetyConfig(true, true, 0.9),
            new SchemaPolicyConfig(SchemaMismatchBehavior.Fail, true, false),
            new[]
            {
                new ConnectionConfig("SourceConn", "Server=.;Database=Src;Integrated Security=true;", EnvironmentType.Dev, ConnectionType.Application),
                new ConnectionConfig("TargetConn", "Server=.;Database=Dst;Integrated Security=true;", EnvironmentType.Dev, ConnectionType.Reporting)
            },
            new[] { job });

        var plan = new ExecutionPlan(
            new[]
            {
                TableIdentifier.Parse("dbo.TargetA"),
                TableIdentifier.Parse("dbo.TargetB"),
                TableIdentifier.Parse("dbo.TargetC")
            },
            new[]
            {
                TableIdentifier.Parse("dbo.TargetC"),
                TableIdentifier.Parse("dbo.TargetB"),
                TableIdentifier.Parse("dbo.TargetA")
            });

        var mapping = CreateMappings(plan.InsertOrder);
        var preflight = new PreFlightResult(job.Name, dryRun: false, new SchemaAnalysisResult(
            new SchemaSnapshot(Array.Empty<TableSchema>(), SchemaRole.Source, SchemaInspectionLevel.Full),
            new SchemaSnapshot(Array.Empty<TableSchema>(), SchemaRole.Target, SchemaInspectionLevel.Full),
            mapping,
            plan));

        var configProvider = new Mock<IConfigurationProvider>(MockBehavior.Strict);
        configProvider.Setup(p => p.LoadAndValidateAsync("path.yaml", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var preFlightValidator = new Mock<IPreFlightValidator>(MockBehavior.Strict);
        preFlightValidator.Setup(v => v.ValidateAsync(config, job, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preflight);

        var calls = new List<(string Phase, string Table)>();
        var runner = new Mock<ITableRunner>(MockBehavior.Strict);
        runner.Setup(r => r.RunPhaseAsync(It.IsAny<TableExecutionContext>(), SyncPhase.Delete, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TableExecutionContext ctx, SyncPhase _, CancellationToken _) =>
            {
                calls.Add(("Delete", $"{ctx.TargetSchema}.{ctx.TargetTable}"));
                return new TableResult($"{ctx.TargetSchema}.{ctx.TargetTable}", TableStatus.Succeeded);
            });
        runner.Setup(r => r.RunPhaseAsync(It.IsAny<TableExecutionContext>(), SyncPhase.Insert, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TableExecutionContext ctx, SyncPhase _, CancellationToken _) =>
            {
                calls.Add(("Insert", $"{ctx.TargetSchema}.{ctx.TargetTable}"));
                return new TableResult($"{ctx.TargetSchema}.{ctx.TargetTable}", TableStatus.Succeeded);
            });

        var orchestrator = new SyncOrchestrator(configProvider.Object, preFlightValidator.Object, runner.Object);

        // Act
        var result = await orchestrator.RunJobAsync("path.yaml", job.Name, null, CancellationToken.None);

        // Assert
        result.Status.Should().Be(JobStatus.Succeeded);
        calls.Should().ContainInOrder(
            ("Delete", "dbo.TargetC"),
            ("Delete", "dbo.TargetB"),
            ("Delete", "dbo.TargetA"),
            ("Insert", "dbo.TargetA"),
            ("Insert", "dbo.TargetB"),
            ("Insert", "dbo.TargetC"));
        runner.VerifyAll();
        configProvider.VerifyAll();
        preFlightValidator.VerifyAll();
    }

    private static SchemaMappingResult CreateMappings(IReadOnlyList<TableIdentifier> targets)
    {
        var dict = new Dictionary<TableIdentifier, TableMapping>();
        var col = new ColumnSchema("Id", typeof(int), "int", false, false, true, null);

        foreach (var target in targets)
        {
            var source = TableIdentifier.Parse($"src.{target.TableName}");
            dict[target] = new TableMapping(
                source,
                target,
                new[] { new ColumnMapping(col, col, MappingKind.OneToOne, null) },
                HasWarnings: false);
        }

        return new SchemaMappingResult(true, Array.Empty<SchemaMappingError>(), dict);
    }
}
