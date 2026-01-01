using FluentAssertions;
using ReportSyncer.Core.Exceptions;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Dependency;
using ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Sync;
using ReportSyncer.Core.Sync.Contracts;
using ReportSyncer.Core.Tests.Helpers.TestDoubles.Security;

namespace ReportSyncer.Core.Tests.UnitTests.Sync;

public class IdentityInsertManagerTests
{
    [Fact]
    public async Task BeginAsync_WhenEnabled_ExecutesOnAndOff()
    {
        var db = new RecordingDbContext();
        var manager = new IdentityInsertManager(_ => db);
        var ctx = CreateContext(enableIdentityInsert: true);

        await using (await manager.BeginAsync(ctx, CancellationToken.None))
        {
            // scope body intentionally empty
        }

        db.ExecutedCommands.Count.Should().Be(2);
        db.ExecutedCommands[0].CommandText.Should().Contain("IDENTITY_INSERT").And.Contain("ON");
        db.ExecutedCommands[1].CommandText.Should().Contain("IDENTITY_INSERT").And.Contain("OFF");
    }

    [Fact]
    public async Task BeginAsync_DisposeOffFailure_ThrowsSyncExecutionException()
    {
        var db = new RecordingDbContext
        {
            OnExecuteNonQueryAsync = cmd =>
            {
                if (cmd.CommandText.Contains("OFF", StringComparison.OrdinalIgnoreCase))
                    return Task.FromException<int>(new InvalidOperationException("off failed"));
                return Task.FromResult(0);
            }
        };

        var manager = new IdentityInsertManager(_ => db);
        var ctx = CreateContext(enableIdentityInsert: true);

        var act = async () =>
        {
            await using var scope = await manager.BeginAsync(ctx, CancellationToken.None);
        };

        var ex = await act.Should().ThrowAsync<SyncExecutionException>();
        ex.And.InnerException.Should().BeOfType<InvalidOperationException>();
        db.ExecutedCommands.Should().Contain(c => c.CommandText.Contains("OFF", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BeginAsync_OnFailure_DoesNotAttemptOff()
    {
        var db = new RecordingDbContext
        {
            OnExecuteNonQueryAsync = cmd =>
            {
                if (cmd.CommandText.Contains("ON", StringComparison.OrdinalIgnoreCase))
                    return Task.FromException<int>(new InvalidOperationException("on failed"));
                return Task.FromResult(0);
            }
        };

        var manager = new IdentityInsertManager(_ => db);
        var ctx = CreateContext(enableIdentityInsert: true);

        var act = () => manager.BeginAsync(ctx, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<SyncExecutionException>();
        ex.And.InnerException.Should().BeOfType<InvalidOperationException>();
        db.ExecutedCommands.Should().HaveCount(1);
        db.ExecutedCommands[0].CommandText.Should().Contain("ON");
        db.ExecutedCommands.Should().NotContain(c => c.CommandText.Contains("OFF", StringComparison.OrdinalIgnoreCase));
    }

    private static TableExecutionContext CreateContext(bool enableIdentityInsert)
    {
        var src = new ColumnSchema("EventTime", typeof(DateTime), "datetime2", false, false, false, null);
        var tgt = new ColumnSchema("EventTime", typeof(DateTime), "datetime2", false, false, false, null);
        var tgtIdentity = new ColumnSchema("EventId", typeof(int), "int", false, true, true, null);

        var mapping = new TableMapping(
            TableIdentifier.Parse("app.SourceEvents"),
            TableIdentifier.Parse("rpt.TargetEvents"),
            new[]
            {
                new ColumnMapping(src, tgt, MappingKind.OneToOne, null),
                new ColumnMapping(null, tgtIdentity, MappingKind.Ignored, null)
            },
            HasWarnings: false);

        return new TableExecutionContext(
            Guid.NewGuid(),
            "Job1",
            "SrcConn",
            "DstConn",
            DatabaseType.SqlServer,
            DatabaseType.SqlServer,
            "rpt",
            "TargetEvents",
            "rpt",
            "TargetEvents",
            dryRun: false,
            preSyncTargetDelete: false,
            enableIdentityInsert: enableIdentityInsert,
            contextColumnName: null,
            contextValue: null,
            filters: Array.Empty<FilterPredicate>(),
            mapping,
            ExecutionPlan.Empty,
            batchSize: 1000, etaSmoothing: null);
    }
}
