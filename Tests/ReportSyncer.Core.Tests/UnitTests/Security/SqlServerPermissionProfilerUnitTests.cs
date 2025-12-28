using FluentAssertions;
using ReportSyncer.Core.Exceptions;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Dependency;
using ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Security;
using ReportSyncer.Core.Sync.Contracts;
using ReportSyncer.Core.Tests.Helpers.TestDoubles.Security;
using System.Linq;

namespace ReportSyncer.Core.Tests.UnitTests.Security;

public class SqlServerPermissionProfilerUnitTests
{
    [Fact]
    public async Task Probe_DryRun_OnlyProbesReadPermissions()
    {
        var source = new RecordingDbContext();
        var target = new RecordingDbContext();
        var profiler = new SqlServerPermissionProfiler(CreateFactory(source, target));
        var ctx = CreateContext(dryRun: true);

        var profile = await profiler.ProbeTablePermissionsAsync(ctx, CancellationToken.None);

        profile.CanReadSource.Should().BeTrue();
        profile.CanReadTarget.Should().BeTrue();
        profile.CanDelete.Should().BeNull();
        profile.CanInsert.Should().BeNull();
        profile.CanSetIdentityInsert.Should().BeNull();
        profile.Notes.Should().Contain("Write permissions are not probed in DryRun.");
        source.ExecutedCommands.Should().HaveCount(1);
        target.ExecutedCommands.Should().HaveCount(1);
    }

    [Fact]
    public async Task Probe_RealRun_PreSyncTargetDeleteFalse_SkipsDeleteProbe()
    {
        var source = new RecordingDbContext();
        var target = new RecordingDbContext();
        var profiler = new SqlServerPermissionProfiler(CreateFactory(source, target));
        var ctx = CreateContext(preSyncTargetDelete: false);

        var profile = await profiler.ProbeTablePermissionsAsync(ctx, CancellationToken.None);

        profile.CanDelete.Should().BeNull();
        profile.Notes.Should().Contain(n => n.Contains("Delete probe skipped", StringComparison.OrdinalIgnoreCase));
        profile.CanInsert.Should().BeTrue();
        profile.CanSetIdentityInsert.Should().BeNull();
        target.ExecutedCommands.Count(c => c.CommandText.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase)).Should().Be(0);
    }

    [Fact]
    public async Task Probe_WhenPermissionDenied_ReturnsFalse_NotThrow()
    {
        var source = new RecordingDbContext();
        var target = new RecordingDbContext
        {
            OnExecuteNonQueryAsync = cmd =>
            {
                if (cmd.CommandText.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase))
                    return Task.FromException<int>(new InvalidOperationException("permission denied to insert"));
                return Task.FromResult(0);
            }
        };

        var profiler = new SqlServerPermissionProfiler(CreateFactory(source, target));
        var ctx = CreateContext(preSyncTargetDelete: false);

        var profile = await profiler.ProbeTablePermissionsAsync(ctx, CancellationToken.None);

        profile.CanInsert.Should().BeFalse();
        profile.Notes.Should().Contain(n => n.Contains("Permission denied", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Probe_WhenNonPermissionSqlError_ThrowsSyncExecutionException()
    {
        var source = new RecordingDbContext();
        var target = new RecordingDbContext
        {
            OnExecuteNonQueryAsync = cmd =>
            {
                if (cmd.CommandText.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase))
                    return Task.FromException<int>(new InvalidOperationException("boom"));
                return Task.FromResult(0);
            }
        };

        var profiler = new SqlServerPermissionProfiler(CreateFactory(source, target));
        var ctx = CreateContext(preSyncTargetDelete: false);

        var act = () => profiler.ProbeTablePermissionsAsync(ctx, CancellationToken.None);

        await act.Should().ThrowAsync<SyncExecutionException>()
            .WithMessage("*TargetEvents*");
    }

    [Fact]
    public async Task IdentityProbe_OnSucceeded_OffFailure_ThrowsAndAttemptsRestore()
    {
        var source = new RecordingDbContext();
        var target = new RecordingDbContext
        {
            OnExecuteNonQueryAsync = cmd =>
            {
                if (cmd.CommandText.Contains("IDENTITY_INSERT", StringComparison.OrdinalIgnoreCase)
                 && cmd.CommandText.Contains("OFF", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromException<int>(new InvalidOperationException("turn off failed"));
                }

                return Task.FromResult(0);
            }
        };

        var profiler = new SqlServerPermissionProfiler(CreateFactory(source, target));
        var ctx = CreateContext(preSyncTargetDelete: false, enableIdentityInsert: true);

        var act = () => profiler.ProbeTablePermissionsAsync(ctx, CancellationToken.None);

        await act.Should().ThrowAsync<SyncExecutionException>();
        target.ExecutedCommands.Count(c => c.CommandText.Contains("IDENTITY_INSERT", StringComparison.OrdinalIgnoreCase))
            .Should().Be(2);
    }

    private static Func<string, DotNetToolkit.Database.Abstractions.IDbContext> CreateFactory(
        RecordingDbContext source,
        RecordingDbContext target)
    {
        return name => string.Equals(name, "Source", StringComparison.OrdinalIgnoreCase) ? source : target;
    }

    private static TableExecutionContext CreateContext(
        bool dryRun = false,
        bool preSyncTargetDelete = true,
        bool enableIdentityInsert = false)
    {
        var srcEventTime = new ColumnSchema("EventTime", typeof(DateTime), "datetime2", false, false, false, null);
        var tgtEventTime = new ColumnSchema("EventTime", typeof(DateTime), "datetime2", false, false, false, null);
        var tgtCustomer = new ColumnSchema("CustomerId", typeof(int), "int", false, false, true, null);
        var tgtPayload = new ColumnSchema("Payload", typeof(string), "nvarchar(100)", true, false, false, 100);
        var tgtIdentity = new ColumnSchema("EventId", typeof(int), "int", false, true, true, null);

        var mapping = new TableMapping(
            TableIdentifier.Parse("app.SourceEvents"),
            TableIdentifier.Parse("rpt.TargetEvents"),
            new[]
            {
                new ColumnMapping(srcEventTime, tgtEventTime, MappingKind.OneToOne, null),
                new ColumnMapping(null, tgtCustomer, MappingKind.ContextColumn, null),
                new ColumnMapping(null, tgtPayload, MappingKind.Constant, "payload"),
                new ColumnMapping(null, tgtIdentity, MappingKind.Ignored, null)
            },
            HasWarnings: false);

        return new TableExecutionContext(
            Guid.NewGuid(),
            "Job1",
            "Source",
            "Target",
            DatabaseType.SqlServer,
            DatabaseType.SqlServer,
            "app",
            "SourceEvents",
            "rpt",
            "TargetEvents",
            dryRun,
            preSyncTargetDelete,
            enableIdentityInsert,
            contextColumnName: "CustomerId",
            contextValue: 50,
            filters: Array.Empty<FilterPredicate>(),
            mapping,
            ExecutionPlan.Empty,
            batchSize: 1000);
    }
}
