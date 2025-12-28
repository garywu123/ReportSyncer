using FluentAssertions;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Dependency;
using ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Security;
using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Core.Tests.IntegrationTests.Security;

[Collection("PermissionProfilerIntegrationTests")]
public class PermissionProfilerIntegrationTests
{
    private readonly PermissionProfilerIntegrationDatabaseFixture _fixture;

    public PermissionProfilerIntegrationTests(PermissionProfilerIntegrationDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task Profiler_AdminUser_AllRequiredFlagsTrue()
    {
        Skip.If(!string.IsNullOrWhiteSpace(_fixture.SkipReason), _fixture.SkipReason);
        var profiler = new SqlServerPermissionProfiler(_fixture.CreateDbContextFactory());
        var ctx = CreateContext(
            sourceConnection: "Admin",
            targetConnection: "Admin",
            preSyncTargetDelete: true,
            enableIdentityInsert: true);

        var profile = await profiler.ProbeTablePermissionsAsync(ctx, CancellationToken.None);

        profile.CanReadSource.Should().BeTrue();
        profile.CanReadTarget.Should().BeTrue();
        profile.CanInsert.Should().BeTrue();
        profile.CanDelete.Should().BeTrue();
        profile.CanSetIdentityInsert.Should().BeTrue();
    }

    [SkippableFact]
    public async Task Profiler_RestrictedUser_DeleteDenied_CanDeleteFalse()
    {
        Skip.If(!string.IsNullOrWhiteSpace(_fixture.SkipReason), _fixture.SkipReason);
        var profiler = new SqlServerPermissionProfiler(_fixture.CreateDbContextFactory());
        var ctx = CreateContext(
            sourceConnection: "Admin",
            targetConnection: "Writer",
            preSyncTargetDelete: true,
            enableIdentityInsert: false);

        var profile = await profiler.ProbeTablePermissionsAsync(ctx, CancellationToken.None);

        profile.CanDelete.Should().BeFalse();
        profile.CanInsert.Should().BeTrue();
        profile.CanSetIdentityInsert.Should().BeNull();
    }

    [SkippableFact]
    public async Task Profiler_RestrictedUser_IdentityInsertDenied_CanSetIdentityInsertFalse()
    {
        Skip.If(!string.IsNullOrWhiteSpace(_fixture.SkipReason), _fixture.SkipReason);
        var profiler = new SqlServerPermissionProfiler(_fixture.CreateDbContextFactory());
        var ctx = CreateContext(
            sourceConnection: "Admin",
            targetConnection: "Writer",
            preSyncTargetDelete: false,
            enableIdentityInsert: true);

        var profile = await profiler.ProbeTablePermissionsAsync(ctx, CancellationToken.None);

        profile.CanSetIdentityInsert.Should().BeFalse();
        profile.CanInsert.Should().BeTrue();
        profile.CanDelete.Should().BeNull();
    }

    private static TableExecutionContext CreateContext(
        string sourceConnection,
        string targetConnection,
        bool preSyncTargetDelete,
        bool enableIdentityInsert)
    {
        var srcEventTime = new ColumnSchema("EventTime", typeof(DateTime), "datetime2", false, false, false, null);
        var tgtEventTime = new ColumnSchema("EventTime", typeof(DateTime), "datetime2", false, false, false, null);
        var tgtCustomer = new ColumnSchema("CustomerId", typeof(int), "int", false, false, true, null);
        var tgtPayload = new ColumnSchema("Payload", typeof(string), "nvarchar(100)", true, false, false, 100);
        var tgtIdentity = new ColumnSchema("EventId", typeof(int), "int", false, true, true, null);

        var mapping = new TableMapping(
            TableIdentifier.Parse("rpt.TargetEvents"),
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
            "PermIT",
            sourceConnection,
            targetConnection,
            DatabaseType.SqlServer,
            DatabaseType.SqlServer,
            "rpt",
            "TargetEvents",
            "rpt",
            "TargetEvents",
            dryRun: false,
            preSyncTargetDelete: preSyncTargetDelete,
            enableIdentityInsert: enableIdentityInsert,
            contextColumnName: "CustomerId",
            contextValue: 50,
            filters: Array.Empty<FilterPredicate>(),
            mapping,
            ExecutionPlan.Empty,
            batchSize: 500);
    }
}
