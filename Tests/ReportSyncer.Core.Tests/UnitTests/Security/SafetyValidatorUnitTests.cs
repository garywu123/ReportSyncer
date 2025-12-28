using FluentAssertions;
using ReportSyncer.Core.Observability;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Dependency;
using ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Security;
using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Core.Tests.UnitTests.Security;

public class SafetyValidatorUnitTests
{
    private readonly SafetyValidatorOptions _options = new SafetyValidatorOptions(50, 1, false);

    [Fact]
    public void Evaluate_RealRun_NoTargetInsert_Blocks()
    {
        var validator = new SafetyValidator(_options);
        var decision = validator.Evaluate(
            CreateContext(dryRun: false, preSyncDelete: false),
            CreateEstimate(deletePct: null, deleteStats: null),
            CreatePermissions(canInsert: false),
            confirmation: null);

        decision.IsAllowed.Should().BeFalse();
        decision.Violations.Should().ContainSingle(v => v.Code == SafetyErrorCodes.NoTargetInsert && v.Severity == SafetySeverity.Blocking);
    }

    [Fact]
    public void Evaluate_DeleteWithoutScope_Default_Blocks()
    {
        var validator = new SafetyValidator(_options);

        var decision = validator.Evaluate(
            CreateContext(preSyncDelete: true, filters: Array.Empty<FilterPredicate>(), contextColumn: null),
            CreateEstimate(deletePct: 0, deleteStats: new EstimatedDeleteStats(10, 0, 0)),
            CreatePermissions(),
            confirmation: null);

        decision.IsAllowed.Should().BeFalse();
        decision.Violations.Should().ContainSingle(v => v.Code == SafetyErrorCodes.DeleteWithoutScope && v.Severity == SafetySeverity.Blocking);
    }

    [Fact]
    public void Evaluate_DeleteWithoutScope_Allowed_DowngradesToWarning()
    {
        var validator = new SafetyValidator(_options with { AllowDeleteWithoutScope = true });

        var decision = validator.Evaluate(
            CreateContext(preSyncDelete: true, filters: Array.Empty<FilterPredicate>(), contextColumn: null),
            CreateEstimate(deletePct: 0, deleteStats: new EstimatedDeleteStats(5, 0, 0)),
            CreatePermissions(),
            confirmation: null);

        decision.IsAllowed.Should().BeTrue();
        decision.Violations.Should().ContainSingle(v => v.Code == SafetyErrorCodes.DeleteWithoutScope && v.Severity == SafetySeverity.Warning);
        decision.Warnings.Should().ContainSingle();
    }

    [Fact]
    public void Evaluate_LargeDelete_RequiresConfirmation_WhenOverThreshold()
    {
        var validator = new SafetyValidator(_options);

        var decision = validator.Evaluate(
            CreateContext(preSyncDelete: true),
            CreateEstimate(deletePct: 60, deleteStats: new EstimatedDeleteStats(10, 6, 60)),
            CreatePermissions(),
            confirmation: null);

        decision.IsAllowed.Should().BeFalse();
        decision.RequiresConfirmation.Should().BeFalse(); // blocked due to missing confirmation
        decision.Violations.Should().ContainSingle(v => v.Code == SafetyErrorCodes.LargeDeleteConfirmationRequired);
    }

    [Fact]
    public void Evaluate_LargeDelete_Confirmed_Allows()
    {
        var validator = new SafetyValidator(_options);
        var confirmation = new SafetyRuntimeConfirmation(true, 60);

        var decision = validator.Evaluate(
            CreateContext(preSyncDelete: true),
            CreateEstimate(deletePct: 60, deleteStats: new EstimatedDeleteStats(10, 6, 60)),
            CreatePermissions(),
            confirmation);

        decision.IsAllowed.Should().BeTrue();
        decision.RequiresConfirmation.Should().BeFalse();
        decision.Violations.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_LargeDelete_ConfirmedPctMismatch_Blocks()
    {
        var validator = new SafetyValidator(_options);
        var confirmation = new SafetyRuntimeConfirmation(true, 10);

        var decision = validator.Evaluate(
            CreateContext(preSyncDelete: true),
            CreateEstimate(deletePct: 60, deleteStats: new EstimatedDeleteStats(10, 6, 60)),
            CreatePermissions(),
            confirmation);

        decision.IsAllowed.Should().BeFalse();
        decision.Violations.Should().ContainSingle(v => v.Code == SafetyErrorCodes.LargeDeleteConfirmationMismatch);
    }

    [Fact]
    public void Evaluate_MissingDeleteEstimate_Blocks()
    {
        var validator = new SafetyValidator(_options);

        var decision = validator.Evaluate(
            CreateContext(preSyncDelete: true),
            CreateEstimate(deletePct: null, deleteStats: null),
            CreatePermissions(),
            confirmation: null);

        decision.IsAllowed.Should().BeFalse();
        decision.Violations.Should().ContainSingle(v => v.Code == SafetyErrorCodes.MissingDeleteEstimate);
    }

    [Fact]
    public void Evaluate_DryRun_SkipsWritePermissionChecks()
    {
        var validator = new SafetyValidator(_options);

        var decision = validator.Evaluate(
            CreateContext(dryRun: true, preSyncDelete: true, filters: new[] { new FilterPredicate("Id", FilterOperator.Equals, 1) }),
            CreateEstimate(deletePct: 10, deleteStats: new EstimatedDeleteStats(10, 1, 10)),
            new PermissionsProfile(canReadSource: true, canReadTarget: true, canDelete: false, canInsert: false, canSetIdentityInsert: false, notes: Array.Empty<string>()),
            confirmation: null);

        decision.IsAllowed.Should().BeTrue();
        decision.Violations.Should().NotContain(v => v.Code == SafetyErrorCodes.NoTargetInsert);
        decision.Violations.Should().NotContain(v => v.Code == SafetyErrorCodes.NoTargetDelete);
    }

    private static PermissionsProfile CreatePermissions(
        bool canReadSource = true,
        bool canReadTarget = true,
        bool? canDelete = true,
        bool? canInsert = true,
        bool? canIdentity = true)
    {
        return new PermissionsProfile(
            canReadSource,
            canReadTarget,
            canDelete,
            canInsert,
            canIdentity,
            Array.Empty<string>());
    }

    private static WorkEstimate CreateEstimate(double? deletePct, EstimatedDeleteStats? deleteStats)
    {
        return new WorkEstimate(
            EstimatedRowsToDelete: deleteStats?.RowsToDelete ?? 0,
            EstimatedRowsToInsert: 10,
            EstimatedDeletePct: deletePct,
            DeleteStats: deleteStats,
            Warnings: Array.Empty<string>());
    }

    private static TableExecutionContext CreateContext(
        bool dryRun = false,
        bool preSyncDelete = false,
        IReadOnlyList<FilterPredicate>? filters = null,
        string? contextColumn = "CustomerId")
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
            "Src",
            "Dst",
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
            batchSize: 1000);
    }
}
