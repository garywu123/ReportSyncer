using System;
using System.Collections.Generic;
using FluentAssertions;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Dependency;
using ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Sync.Contracts;
using ReportSyncer.Core.Sync.Sql;
using Xunit;

namespace ReportSyncer.Core.Tests.UnitTests.Sync.Sql;

public class SqlServerQueryBuilderTests
{
    [Fact]
    public void WhereClauseBuilder_WithTwoPredicates_UsesAndAndCreatesTwoParameters()
    {
        // Arrange
        var filters = new List<FilterPredicate>
        {
            new("CustomerId", FilterOperator.Equals, 50),
            new("OrderDate", FilterOperator.GreaterOrEqual, new DateTime(2025, 1, 1))
        };
        var builder = new WhereClauseBuilder();

        // Act
        var result = builder.Build(filters);

        // Assert
        result.Sql.Should().Be("WHERE [CustomerId] = @p0 AND [OrderDate] >= @p1");
        result.Parameters.Should().HaveCount(2);
        result.Parameters[0].Value.Should().Be(50);
    }

    [Fact]
    public void BuildDelete_WithNoFilters_ThrowsConfigurationException()
    {
        // Arrange
        var ctx = new DeleteCommandContext("dbo", "Target", Array.Empty<FilterPredicate>());
        var builder = new SqlServerQueryBuilder();

        // Act
        var act = () => builder.BuildDelete(ctx);

        // Assert
        act.Should().Throw<ConfigurationException>();
    }

    [Fact]
    public void BuildCountEstimate_WithNoFilters_NoWhereClause()
    {
        // Arrange
        var execCtx = CreateExecutionContext(filters: Array.Empty<FilterPredicate>());
        var builder = new SqlServerQueryBuilder();

        // Act
        var spec = builder.BuildCountEstimate(execCtx);

        // Assert
        spec.Sql.Should().Be("SELECT COUNT(1) FROM [dbo].[Target]");
        spec.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void BuildSelectSource_DoesNotIncludeContextColumn()
    {
        // Arrange
        var sourceCol = new ColumnSchema("OrderId", typeof(long), "bigint", isNullable: false, isIdentity: true, isPrimaryKeyPart: true, maxLength: null);
        var targetCol = new ColumnSchema("OrderId", typeof(long), "bigint", isNullable: false, isIdentity: true, isPrimaryKeyPart: true, maxLength: null);
        var contextTarget = new ColumnSchema("CustomerId", typeof(int), "int", isNullable: false, isIdentity: false, isPrimaryKeyPart: true, maxLength: null);

        var mapping = new TableMapping(
            TableIdentifier.Parse("dbo.Source"),
            TableIdentifier.Parse("dbo.Target"),
            new[]
            {
                new ColumnMapping(sourceCol, targetCol, MappingKind.OneToOne, null),
                new ColumnMapping(null, contextTarget, MappingKind.ContextColumn, null)
            },
            HasWarnings: false);

        var insertCtx = new InsertCommandContext(
            "dbo",
            "Source",
            "dbo",
            "Target",
            Array.Empty<FilterPredicate>(),
            contextColumnName: "CustomerId",
            contextValue: 50,
            mapping,
            enableIdentityInsert: false,
            batchSize: 100);

        var builder = new SqlServerQueryBuilder();

        // Act
        var spec = builder.BuildSelectSource(insertCtx);

        // Assert
        spec.Sql.Should().Be("SELECT [OrderId] FROM [dbo].[Source]");
        spec.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void BuildInsertTarget_WithContextInjection_AppendsColumnAndParameter()
    {
        // Arrange
        var sourceCol = new ColumnSchema("OrderId", typeof(long), "bigint", isNullable: false, isIdentity: true, isPrimaryKeyPart: true, maxLength: null);
        var targetCol = new ColumnSchema("OrderId", typeof(long), "bigint", isNullable: false, isIdentity: true, isPrimaryKeyPart: true, maxLength: null);
        var contextTarget = new ColumnSchema("CustomerId", typeof(int), "int", isNullable: false, isIdentity: false, isPrimaryKeyPart: true, maxLength: null);

        var mapping = new TableMapping(
            TableIdentifier.Parse("dbo.Source"),
            TableIdentifier.Parse("dbo.Target"),
            new[]
            {
                new ColumnMapping(sourceCol, targetCol, MappingKind.OneToOne, null),
                new ColumnMapping(null, contextTarget, MappingKind.ContextColumn, null)
            },
            HasWarnings: false);

        var insertCtx = new InsertCommandContext(
            "dbo",
            "Source",
            "dbo",
            "Target",
            Array.Empty<FilterPredicate>(),
            contextColumnName: "CustomerId",
            contextValue: 50,
            mapping,
            enableIdentityInsert: false,
            batchSize: 100);

        var builder = new SqlServerQueryBuilder();

        // Act
        var spec = builder.BuildInsertTarget(insertCtx);

        // Assert
        spec.Sql.Should().Contain("[CustomerId]");
        spec.Parameters.Should().ContainSingle(p => Equals(p.Value, 50));
    }

    [Fact]
    public void SqlIdentifier_WhenNameContainsClosingBracket_Throws()
    {
        // Act
        var act = () => SqlIdentifier.EscapeIdentifier("Bad]Name");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    private static TableExecutionContext CreateExecutionContext(IReadOnlyList<FilterPredicate> filters)
    {
        var tableMapping = new TableMapping(
            TableIdentifier.Parse("dbo.Source"),
            TableIdentifier.Parse("dbo.Target"),
            Array.Empty<ColumnMapping>(),
            HasWarnings: false);

        return new TableExecutionContext(
            Guid.NewGuid(),
            "Job1",
            "SourceConn",
            "TargetConn",
            DatabaseType.SqlServer,
            DatabaseType.SqlServer,
            "dbo",
            "Source",
            "dbo",
            "Target",
            dryRun: false,
            preSyncTargetDelete: false,
            enableIdentityInsert: false,
            contextColumnName: null,
            contextValue: null,
            filters,
            tableMapping,
            ExecutionPlan.Empty,
            batchSize: 1000,
            etaSmoothing: null);
    }
}
