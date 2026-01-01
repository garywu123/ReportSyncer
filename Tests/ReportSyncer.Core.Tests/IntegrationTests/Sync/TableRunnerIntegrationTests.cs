using FluentAssertions;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Dependency;
using ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Sync;
using ReportSyncer.Core.Sync.Contracts;
using ReportSyncer.Core.Sync.Sql;

namespace ReportSyncer.Core.Tests.IntegrationTests.Sync;

[Collection("TableRunnerIntegrationTests")]
public class TableRunnerIntegrationTests
{
    private readonly TableRunnerIntegrationFixture _fixture;

    public TableRunnerIntegrationTests(TableRunnerIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task RunAsync_DeletesScopedRows_AndInsertsFromSource()
    {
        Skip.If(!string.IsNullOrWhiteSpace(_fixture.SkipReason), _fixture.SkipReason);
        await _fixture.ResetDataAsync();

        var connectionFactory = _fixture.CreateConnectionFactory();
        var dbContextFactory = _fixture.CreateDbContextFactory();
        var builder = new SqlServerQueryBuilder();
        var identityManager = new IdentityInsertManager(dbContextFactory);
        var writer = new SqlDataWriter(dbContextFactory, builder, identityManager);
        var runner = new TableRunner(connectionFactory, writer, builder);
        var ctx = CreateContext();

        var result = await runner.RunAsync(ctx, CancellationToken.None);

        result.Status.Should().Be(TableStatus.Succeeded);
        result.RowsDeleted.Should().BeGreaterThan(0);
        result.RowsInserted.Should().Be(2);

        var customer1 = await _fixture.LoadTargetAsync(1);
        customer1.Should().HaveCount(2);
        customer1.Select(r => r.EventId).Should().BeEquivalentTo(new[] { 100, 101 });
        customer1.Select(r => r.Payload).Should().BeEquivalentTo(new[] { "src-a", "src-b" });

        var customer2Count = await _fixture.CountAsync("SELECT COUNT(*) FROM rpt.TargetEvents WHERE CustomerId = 2;");
        customer2Count.Should().Be(1);
    }

    private static TableExecutionContext CreateContext()
    {
        var srcCustomer = new ColumnSchema("CustomerId", typeof(int), "int", false, false, true, null);
        var tgtCustomer = new ColumnSchema("CustomerId", typeof(int), "int", false, false, true, null);
        var srcEventId = new ColumnSchema("EventId", typeof(int), "int", false, true, true, null);
        var tgtEventId = new ColumnSchema("EventId", typeof(int), "int", false, true, true, null);
        var srcEventTime = new ColumnSchema("EventTime", typeof(DateTime), "datetime2", false, false, false, null);
        var tgtEventTime = new ColumnSchema("EventTime", typeof(DateTime), "datetime2", false, false, false, null);
        var srcPayload = new ColumnSchema("Payload", typeof(string), "nvarchar", true, false, false, 100);
        var tgtPayload = new ColumnSchema("Payload", typeof(string), "nvarchar", true, false, false, 100);

        var mapping = new TableMapping(
            TableIdentifier.Parse("src.SourceEvents"),
            TableIdentifier.Parse("rpt.TargetEvents"),
            new[]
            {
                new ColumnMapping(srcCustomer, tgtCustomer, MappingKind.OneToOne, null),
                new ColumnMapping(srcEventId, tgtEventId, MappingKind.OneToOne, null),
                new ColumnMapping(srcEventTime, tgtEventTime, MappingKind.OneToOne, null),
                new ColumnMapping(srcPayload, tgtPayload, MappingKind.OneToOne, null)
            },
            HasWarnings: false);

        return new TableExecutionContext(
            Guid.NewGuid(),
            "TableRunnerIT",
            sourceConnectionName: "Source",
            targetConnectionName: "Target",
            sourceDbType: DatabaseType.SqlServer,
            targetDbType: DatabaseType.SqlServer,
            sourceSchema: "src",
            sourceTable: "SourceEvents",
            targetSchema: "rpt",
            targetTable: "TargetEvents",
            dryRun: false,
            preSyncTargetDelete: true,
            enableIdentityInsert: true,
            contextColumnName: null,
            contextValue: null,
            filters: new[] { new FilterPredicate("CustomerId", FilterOperator.Equals, 1) },
            tableMapping: mapping,
            executionPlan: ExecutionPlan.Empty,
            batchSize: 500, etaSmoothing: null);
    }
}
