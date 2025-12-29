using FluentAssertions;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Dependency;
using ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Sync;
using ReportSyncer.Core.Sync.Contracts;
using ReportSyncer.Core.Sync.Sql;

namespace ReportSyncer.Core.Tests.IntegrationTests.Sync;

[Collection("SqlDataWriterIntegrationTests")]
public class SqlDataWriterIntegrationTests
{
    private readonly SqlDataWriterIntegrationFixture _fixture;

    public SqlDataWriterIntegrationTests(SqlDataWriterIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task Delete_WithFilter_RemovesOnlyMatchingRows()
    {
        Skip.If(!string.IsNullOrWhiteSpace(_fixture.SkipReason), _fixture.SkipReason);
        await _fixture.ResetTableAsync();
        var writer = CreateWriter();
        var ctx = CreateContext(preSyncTargetDelete: true, filters: new[] { new FilterPredicate("CustomerId", FilterOperator.Equals, 1) }, enableIdentityInsert: false, batchSize: 500);

        var deleted = await writer.DeleteAsync(ctx, CancellationToken.None);

        deleted.Should().Be(3);
        var remainingCustomer1 = await _fixture.ExecuteScalarAsync("SELECT COUNT(*) FROM rpt.TargetEvents WHERE CustomerId = 1;");
        var remainingCustomer2 = await _fixture.ExecuteScalarAsync("SELECT COUNT(*) FROM rpt.TargetEvents WHERE CustomerId = 2;");
        remainingCustomer1.Should().Be(0);
        remainingCustomer2.Should().Be(2);
    }

    [SkippableFact]
    public async Task Insert_WithIdentityInsertEnabled_PreservesExplicitIdentityValues()
    {
        Skip.If(!string.IsNullOrWhiteSpace(_fixture.SkipReason), _fixture.SkipReason);
        await _fixture.ResetTableAsync();

        var writer = CreateWriter();
        var ctx = CreateContext(preSyncTargetDelete: false, filters: Array.Empty<FilterPredicate>(), enableIdentityInsert: true, batchSize: 2);

        var rows = new List<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["CustomerId"] = 10, ["EventId"] = 100, ["EventTime"] = new DateTime(2025,12,25), ["Payload"] = "x" },
            new Dictionary<string, object?> { ["CustomerId"] = 10, ["EventId"] = 101, ["EventTime"] = new DateTime(2025,12,26), ["Payload"] = "y" },
            new Dictionary<string, object?> { ["CustomerId"] = 11, ["EventId"] = 200, ["EventTime"] = new DateTime(2025,12,27), ["Payload"] = "z" }
        };

        var inserted = await writer.InsertAsync(ctx, rows, CancellationToken.None);

        inserted.Should().Be(3);
        var explicitIds = await _fixture.ExecuteScalarAsync("SELECT COUNT(*) FROM rpt.TargetEvents WHERE EventId IN (100,101,200);");
        explicitIds.Should().Be(3);
    }

    private SqlDataWriter CreateWriter()
    {
        var dbFactory = _fixture.CreateDbContextFactory();
        var builder = new SqlServerQueryBuilder();
        var identity = new IdentityInsertManager(dbFactory);
        return new SqlDataWriter(dbFactory, builder, identity);
    }

    private static TableExecutionContext CreateContext(
        bool preSyncTargetDelete,
        IReadOnlyList<FilterPredicate> filters,
        bool enableIdentityInsert,
        int batchSize)
    {
        var srcEventTime = new ColumnSchema("EventTime", typeof(DateTime), "datetime2", false, false, false, null);
        var tgtEventTime = new ColumnSchema("EventTime", typeof(DateTime), "datetime2", false, false, false, null);
        var tgtPayload = new ColumnSchema("Payload", typeof(string), "nvarchar(100)", true, false, false, 100);
        var tgtCustomer = new ColumnSchema("CustomerId", typeof(int), "int", false, false, true, null);
        var tgtIdentity = new ColumnSchema("EventId", typeof(int), "int", false, true, true, null);

        var mapping = new TableMapping(
            TableIdentifier.Parse("app.SourceEvents"),
            TableIdentifier.Parse("rpt.TargetEvents"),
            new[]
            {
                new ColumnMapping(srcEventTime, tgtEventTime, MappingKind.OneToOne, null),
                new ColumnMapping(null, tgtPayload, MappingKind.Constant, "payload"),
                new ColumnMapping(null, tgtCustomer, MappingKind.ContextColumn, null),
                new ColumnMapping(tgtIdentity, tgtIdentity, MappingKind.OneToOne, null)
            },
            HasWarnings: false);

        return new TableExecutionContext(
            Guid.NewGuid(),
            "SqlDataWriterIT",
            sourceConnectionName: "Target",
            targetConnectionName: "Target",
            sourceDbType: DatabaseType.SqlServer,
            targetDbType: DatabaseType.SqlServer,
            sourceSchema: "app",
            sourceTable: "SourceEvents",
            targetSchema: "rpt",
            targetTable: "TargetEvents",
            dryRun: false,
            preSyncTargetDelete: preSyncTargetDelete,
            enableIdentityInsert: enableIdentityInsert,
            contextColumnName: "CustomerId",
            contextValue: 1,
            filters: filters,
            tableMapping: mapping,
            executionPlan: ExecutionPlan.Empty,
            batchSize: batchSize);
    }
}
