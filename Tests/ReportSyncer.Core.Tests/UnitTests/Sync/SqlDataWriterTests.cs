using FluentAssertions;
using Moq;
using ReportSyncer.Core.Exceptions;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Dependency;
using ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Sync;
using ReportSyncer.Core.Sync.Contracts;
using ReportSyncer.Core.Sync.Sql;
using ReportSyncer.Core.Tests.Helpers.TestDoubles.Security;
using ReportSyncer.Core.Tests.Helpers.TestDoubles.Sql;

namespace ReportSyncer.Core.Tests.UnitTests.Sync;

public class SqlDataWriterTests
{
    [Fact]
    public async Task DeleteAsync_ExecutesDeleteAndReturnsAffected()
    {
        var db = new RecordingDbContext
        {
            OnExecuteNonQueryAsync = _ => Task.FromResult(5)
        };

        var writer = CreateWriter(_ => db);
        var ctx = CreateContext(preSyncDelete: true);

        var deleted = await writer.DeleteAsync(ctx, CancellationToken.None);

        deleted.Should().Be(5);
        db.ExecutedCommands.Should().ContainSingle(c => c.CommandText.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task InsertAsync_SplitsIntoBatches()
    {
        var db = new RecordingDbContext
        {
            OnExecuteNonQueryAsync = cmd =>
            {
                // infer row count from parameter count / columns
                var rowCount = cmd.Parameters.Count() / 3;
                return Task.FromResult(rowCount);
            }
        };

        var writer = CreateWriter(_ => db);
        var ctx = CreateContext(enableIdentityInsert: false, batchSize: 2);
        var rows = new List<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["CustomerId"] = 1, ["EventTime"] = DateTime.UtcNow, ["Payload"] = "a" },
            new Dictionary<string, object?> { ["CustomerId"] = 2, ["EventTime"] = DateTime.UtcNow, ["Payload"] = "b" },
            new Dictionary<string, object?> { ["CustomerId"] = 3, ["EventTime"] = DateTime.UtcNow, ["Payload"] = "c" },
        };

        var inserted = await writer.InsertAsync(ctx, rows, CancellationToken.None);

        inserted.Should().Be(3);
        db.ExecutedCommands.Should().HaveCount(2);
        db.ExecutedCommands[0].Parameters.Should().NotBeEmpty();
    }

    [Fact]
    public async Task InsertAsync_ThrowsWhenMissingColumn()
    {
        var writer = CreateWriter(_ => new RecordingDbContext());
        var ctx = CreateContext(enableIdentityInsert: false, batchSize: 1);
        var rows = new List<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["CustomerId"] = 10 } // missing EventTime
        };

        var act = () => writer.InsertAsync(ctx, rows, CancellationToken.None);

        await act.Should().ThrowAsync<SyncExecutionException>()
            .WithMessage("*EventTime*");
    }

    private static SqlDataWriter CreateWriter(Func<string, RecordingDbContext> factory)
    {
        var builder = new SqlServerQueryBuilder();
        var identityManager = new IdentityInsertManager(name => factory(name));
        return new SqlDataWriter(name => factory(name), builder, identityManager);
    }

    private static TableExecutionContext CreateContext(
        bool preSyncDelete = false,
        bool enableIdentityInsert = false,
        int batchSize = 2)
    {
        var srcEventTime = new ColumnSchema("EventTime", typeof(DateTime), "datetime2", false, false, false, null);
        var tgtEventTime = new ColumnSchema("EventTime", typeof(DateTime), "datetime2", false, false, false, null);
        var tgtPayload = new ColumnSchema("Payload", typeof(string), "nvarchar(100)", true, false, false, 100);
        var tgtCustomer = new ColumnSchema("CustomerId", typeof(int), "int", false, false, true, null);

        var mapping = new TableMapping(
            TableIdentifier.Parse("app.SourceEvents"),
            TableIdentifier.Parse("rpt.TargetEvents"),
            new[]
            {
                new ColumnMapping(srcEventTime, tgtEventTime, MappingKind.OneToOne, null),
                new ColumnMapping(null, tgtPayload, MappingKind.Constant, "payload"),
                new ColumnMapping(null, tgtCustomer, MappingKind.ContextColumn, null)
            },
            HasWarnings: false);

        return new TableExecutionContext(
            Guid.NewGuid(),
            "Job1",
            "Dst",
            "Dst",
            DatabaseType.SqlServer,
            DatabaseType.SqlServer,
            "app",
            "SourceEvents",
            "rpt",
            "TargetEvents",
            dryRun: false,
            preSyncTargetDelete: preSyncDelete,
            enableIdentityInsert: enableIdentityInsert,
            contextColumnName: "CustomerId",
            contextValue: 42,
            filters: Array.Empty<FilterPredicate>(),
            mapping,
            ExecutionPlan.Empty,
            batchSize: batchSize);
    }
}
