using System.Collections;
using System.Data;
using FluentAssertions;
using Moq;
using DotNetToolkit.Database.Abstractions;
using ReportSyncer.Core.Observability;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Dependency;
using ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Sync.Contracts;
using ReportSyncer.Core.Sync.Sql;
using ReportSyncer.Core.Tests.Helpers.TestDoubles.Sql;

namespace ReportSyncer.Core.Tests.UnitTests.Observability;

public class WorkEstimatorUnitTests
{
    [Fact]
    public async Task EstimateAsync_PreSyncTargetDeleteFalse_ReturnsInsertOnly()
    {
        // Arrange
        var sourceFactory = new ScalarQueueDbConnectionFactory(10);
        var targetFactory = new ScalarQueueDbConnectionFactory();
        var sqlBuilder = CreateSqlBuilder();
        var ctx = CreateContext(preSyncTargetDelete: false);
        var estimator = new WorkEstimator(sourceFactory, targetFactory, sqlBuilder.Object);

        // Act
        var result = await estimator.EstimateAsync(ctx, CancellationToken.None);

        // Assert
        result.EstimatedRowsToInsert.Should().Be(10);
        result.EstimatedRowsToDelete.Should().Be(0);
        result.DeleteStats.Should().BeNull();
        result.EstimatedDeletePct.Should().BeNull();
    }

    [Fact]
    public async Task EstimateAsync_PreSyncTargetDeleteTrue_ComputesDeleteAndPct()
    {
        // Arrange
        var sourceFactory = new ScalarQueueDbConnectionFactory(5);
        var targetFactory = new ScalarQueueDbConnectionFactory(4, 8); // delete rows, total rows
        var sqlBuilder = CreateSqlBuilder();
        var ctx = CreateContext(preSyncTargetDelete: true);
        var estimator = new WorkEstimator(sourceFactory, targetFactory, sqlBuilder.Object);

        // Act
        var result = await estimator.EstimateAsync(ctx, CancellationToken.None);

        // Assert
        result.EstimatedRowsToInsert.Should().Be(5);
        result.EstimatedRowsToDelete.Should().Be(4);
        result.DeleteStats.Should().NotBeNull();
        result.DeleteStats!.TargetTotalRows.Should().Be(8);
        result.EstimatedDeletePct.Should().BeApproximately(50.0, 0.001);
    }

    [Fact]
    public async Task EstimateAsync_AppendsContextPredicateToTargetDeleteScope()
    {
        // Arrange
        var sourceFactory = new ScalarQueueDbConnectionFactory(1);
        var targetFactory = new ScalarQueueDbConnectionFactory(2, 4);

        var captured = new List<TableExecutionContext>();
        var sqlBuilder = CreateSqlBuilder(ctx =>
        {
            captured.Add(ctx);
            return new DbCommandSpec("SELECT 1", Array.Empty<CommandParameterSpec>());
        });

        var ctx = CreateContext(preSyncTargetDelete: true, contextColumn: "CustomerId", contextValue: 50);
        var estimator = new WorkEstimator(sourceFactory, targetFactory, sqlBuilder.Object);

        // Act
        await estimator.EstimateAsync(ctx, CancellationToken.None);

        // Assert
        captured.Should().NotBeEmpty();
        captured.Should().Contain(c => c.Filters.Any(f => f.ColumnName == "CustomerId" && Equals(f.Value, 50)));
    }

    [Fact]
    public async Task EstimateAsync_WhenCountQueryFails_ThrowsWorkEstimationException()
    {
        // Arrange
        var sourceFactory = new ThrowingDbConnectionFactory(new InvalidOperationException("boom"));
        var targetFactory = new ScalarQueueDbConnectionFactory();
        var sqlBuilder = CreateSqlBuilder();
        var ctx = CreateContext(preSyncTargetDelete: false);
        var estimator = new WorkEstimator(sourceFactory, targetFactory, sqlBuilder.Object);

        // Act
        var act = () => estimator.EstimateAsync(ctx, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<WorkEstimationException>()
            .WithMessage("*Job1*Target*Estimate*");
    }

    private static TableExecutionContext CreateContext(bool preSyncTargetDelete, string? contextColumn = null, object? contextValue = null)
    {
        var sourceCol = new ColumnSchema("EventTime", typeof(DateTime), "datetime2", false, false, false, null);
        var targetCol = new ColumnSchema("EventTime", typeof(DateTime), "datetime2", false, false, false, null);
        var mapping = new TableMapping(
            TableIdentifier.Parse("app.SourceEvents"),
            TableIdentifier.Parse("rpt.TargetEvents"),
            new[] { new ColumnMapping(sourceCol, targetCol, MappingKind.OneToOne, null) },
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
            dryRun: true,
            preSyncTargetDelete: preSyncTargetDelete,
            enableIdentityInsert: false,
            contextColumnName: contextColumn,
            contextValue: contextValue,
            filters: Array.Empty<FilterPredicate>(),
            mapping,
            ExecutionPlan.Empty,
            batchSize: 1000);
    }

    private static Mock<ISqlQueryBuilder> CreateSqlBuilder(Func<TableExecutionContext, DbCommandSpec>? countFactory = null)
    {
        var builder = new Mock<ISqlQueryBuilder>();
        builder.Setup(b => b.BuildSelectSource(It.IsAny<InsertCommandContext>()))
            .Returns(new DbCommandSpec("SELECT 1", Array.Empty<CommandParameterSpec>()));

        builder.Setup(b => b.BuildCountEstimate(It.IsAny<TableExecutionContext>()))
            .Returns((TableExecutionContext c) => countFactory?.Invoke(c) ?? new DbCommandSpec("SELECT 1", Array.Empty<CommandParameterSpec>()));
        return builder;
    }

}
