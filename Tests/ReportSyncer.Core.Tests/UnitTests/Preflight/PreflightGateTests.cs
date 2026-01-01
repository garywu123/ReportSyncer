using FluentAssertions;
using Moq;
using ReportSyncer.Core.Exceptions;
using ReportSyncer.Core.Observability;
using ReportSyncer.Core.Preflight;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Dependency;
using ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Security;
using ReportSyncer.Core.Sync.Contracts;
using ReportSyncer.Core.Sync.Sql;
using ReportSyncer.Core.Tests.Helpers.TestDoubles.Sql;

namespace ReportSyncer.Core.Tests.UnitTests.Preflight;

public class PreflightGateTests
{
    [Fact]
    public async Task EvaluateAsync_WhenSafetyBlocks_OverallIsAllowedFalse()
    {
        var estimator = CreateEstimator(5);
        var permissionProfiler = CreatePermissionProfiler(canInsert: false);
        var safety = new SafetyValidator(new SafetyValidatorOptions(100, 1, false));
        var gate = new PreflightGate(estimator, permissionProfiler.Object, safety);
        var ctx = CreateContext(dryRun: false, preSyncDelete: false);

        var result = await gate.EvaluateAsync(new[] { ctx }, confirmation: null, ct: CancellationToken.None);

        result.IsAllowed.Should().BeFalse();
        result.BlockingCodes.Should().Contain(SafetyErrorCodes.NoTargetInsert);
        result.TableReports.Should().ContainSingle(r => r.IsAllowed == false);
    }

    [Fact]
    public async Task EvaluateAsync_AggregatesNotesFromEstimatePermissionsAndSafety()
    {
        var estimator = CreateEstimator(sourceCount: 1, targetCounts: new[] { 0L, 1L });
        var permissionProfiler = CreatePermissionProfiler(canInsert: true, notes: new[] { "perm-note" });
        var safety = new SafetyValidator(new SafetyValidatorOptions(100, 1, true));
        var gate = new PreflightGate(estimator, permissionProfiler.Object, safety);
        var ctx = CreateContext(dryRun: false, preSyncDelete: true, contextColumn: null, filters: Array.Empty<FilterPredicate>());

        var result = await gate.EvaluateAsync(new[] { ctx }, confirmation: null, ct: CancellationToken.None);

        var report = result.TableReports.Single();
        report.IsAllowed.Should().BeTrue();
        report.Notes.Should().Contain(n => n.Contains("Delete scope"));
        report.Notes.Should().Contain("perm-note");
        report.Notes.Should().Contain(n => n.Contains("Pre-sync delete is enabled"));
    }

    [Fact]
    public async Task EvaluateAsync_WhenEstimatorThrows_WrapsPreflightGateException()
    {
        var builder = CreateSqlBuilder();
        var estimator = new WorkEstimator(
            new ThrowingDbConnectionFactory(new InvalidOperationException("boom")),
            new ThrowingDbConnectionFactory(new InvalidOperationException("boom")),
            builder.Object);

        var permissionProfiler = CreatePermissionProfiler();
        var safety = new SafetyValidator(new SafetyValidatorOptions(100, 1, false));
        var gate = new PreflightGate(estimator, permissionProfiler.Object, safety);
        var ctx = CreateContext(dryRun: false, preSyncDelete: false);

        var act = () => gate.EvaluateAsync(new[] { ctx }, confirmation: null, ct: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<PreflightGateException>();
        ex.And.InnerException.Should().BeOfType<WorkEstimationException>();
    }

    [Fact]
    public async Task EvaluateAsync_WhenProfilerThrows_WrapsPreflightGateException()
    {
        var estimator = CreateEstimator(1);
        var permissionProfiler = new Mock<IPermissionProfiler>();
        permissionProfiler
            .Setup(p => p.ProbeTablePermissionsAsync(It.IsAny<TableExecutionContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SyncExecutionException("no permissions"));

        var safety = new SafetyValidator(new SafetyValidatorOptions(100, 1, false));
        var gate = new PreflightGate(estimator, permissionProfiler.Object, safety);
        var ctx = CreateContext(dryRun: false, preSyncDelete: false);

        var act = () => gate.EvaluateAsync(new[] { ctx }, confirmation: null, ct: CancellationToken.None);

        var ex = await act.Should().ThrowAsync<PreflightGateException>();
        ex.And.InnerException.Should().BeOfType<SyncExecutionException>();
    }

    [Fact]
    public async Task EvaluateAsync_DeduplicatesBlockingCodes()
    {
        var estimator = CreateEstimator(2, targetCounts: new[] { 0L, 0L });
        var permissionProfiler = CreatePermissionProfiler(canInsert: false);
        var safety = new SafetyValidator(new SafetyValidatorOptions(100, 1, false));
        var gate = new PreflightGate(estimator, permissionProfiler.Object, safety);
        var ctx1 = CreateContext(dryRun: false, preSyncDelete: false);
        var ctx2 = CreateContext(dryRun: false, preSyncDelete: false);

        var result = await gate.EvaluateAsync(new[] { ctx1, ctx2 }, confirmation: null, ct: CancellationToken.None);

        result.IsAllowed.Should().BeFalse();
        result.BlockingCodes.Count.Should().Be(1);
        result.BlockingCodes.Should().Contain(SafetyErrorCodes.NoTargetInsert);
    }

    private static PreflightGate CreateGate(WorkEstimator estimator, IPermissionProfiler profiler, ISafetyValidator safety)
        => new(estimator, profiler, safety);

    private static WorkEstimator CreateEstimator(long sourceCount, long[]? targetCounts = null)
    {
        var builder = CreateSqlBuilder();
        var sourceFactory = new ScalarQueueDbConnectionFactory(sourceCount);
        var targetFactory = new ScalarQueueDbConnectionFactory((targetCounts ?? Array.Empty<long>()).Cast<object?>().ToArray());
        return new WorkEstimator(sourceFactory, targetFactory, builder.Object);
    }

    private static Mock<ISqlQueryBuilder> CreateSqlBuilder()
    {
        var builder = new Mock<ISqlQueryBuilder>();
        builder.Setup(b => b.BuildSelectSource(It.IsAny<InsertCommandContext>()))
            .Returns(new DbCommandSpec("SELECT 1", Array.Empty<CommandParameterSpec>()));
        builder.Setup(b => b.BuildCountEstimate(It.IsAny<TableExecutionContext>()))
            .Returns(new DbCommandSpec("SELECT 1", Array.Empty<CommandParameterSpec>()));
        return builder;
    }

    private static Mock<IPermissionProfiler> CreatePermissionProfiler(bool canInsert = true, IEnumerable<string>? notes = null)
    {
        var profiler = new Mock<IPermissionProfiler>();
        profiler.Setup(p => p.ProbeTablePermissionsAsync(It.IsAny<TableExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PermissionsProfile(
                canReadSource: true,
                canReadTarget: true,
                canDelete: true,
                canInsert: canInsert,
                canSetIdentityInsert: true,
                notes: notes?.ToArray() ?? Array.Empty<string>()));
        return profiler;
    }

    private static TableExecutionContext CreateContext(
        bool dryRun,
        bool preSyncDelete,
        string? contextColumn = "CustomerId",
        IReadOnlyList<FilterPredicate>? filters = null)
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
            "app",
            "SourceEvents",
            "rpt",
            "TargetEvents",
            dryRun,
            preSyncDelete,
            enableIdentityInsert: false,
            contextColumnName: contextColumn,
            contextValue: 42,
            filters: filters ?? Array.Empty<FilterPredicate>(),
            mapping,
            ExecutionPlan.Empty,
            batchSize: 1000, etaSmoothing: null);
    }
}
