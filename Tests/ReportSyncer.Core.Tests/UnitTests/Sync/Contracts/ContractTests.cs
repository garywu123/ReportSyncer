using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Dependency;
using ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Core.Tests.UnitTests.Sync.Contracts;

public class ContractTests
{
    [Fact]
    public void TableExecutionContext_WhenCreated_HasAllRequiredFields()
    {
        // Arrange
        var filters = new List<FilterPredicate> { new("Id", FilterOperator.Equals, 1) };
        var tableMapping = new TableMapping(
            TableIdentifier.Parse("dbo.Source"),
            TableIdentifier.Parse("dbo.Target"),
            Array.Empty<ColumnMapping>(),
            HasWarnings: false);
        var executionPlan = new ExecutionPlan(
            new[] { TableIdentifier.Parse("dbo.Target") },
            new[] { TableIdentifier.Parse("dbo.Target") });

        // Act
        var context = new TableExecutionContext(
            Guid.NewGuid(),
            "Job-1",
            "SourceConn",
            "TargetConn",
            DatabaseType.SqlServer,
            DatabaseType.SqlServer,
            "dbo",
            "Source",
            "dbo",
            "Target",
            dryRun: true,
            preSyncTargetDelete: false,
            enableIdentityInsert: true,
            contextColumnName: "CustomerId",
            contextValue: 50,
            filters,
            tableMapping,
            executionPlan,
            batchSize: 100, etaSmoothing: null);

        // Assert
        context.JobName.Should().Be("Job-1");
        context.SourceSchema.Should().Be("dbo");
        context.TargetTable.Should().Be("Target");
        context.Filters.Should().HaveCount(1);
        context.Filters.Should().NotBeSameAs(filters);
        context.TableMapping.Should().BeSameAs(tableMapping);
        context.ExecutionPlan.Should().BeSameAs(executionPlan);
    }

    [Fact]
    public void FilterPredicate_WhenColumnNameBlank_ThrowsArgumentException()
    {
        // Arrange
        var act = () => new FilterPredicate(" ", FilterOperator.Equals, 1);

        // Act & Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FilterPredicate_BetweenInclusive_RequiresValue2()
    {
        // Arrange
        var act = () => new FilterPredicate("Date", FilterOperator.BetweenInclusive, new DateTime(2025, 1, 1), null);

        // Act & Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DbCommandSpec_DoesNotMutateParametersCollection()
    {
        // Arrange
        var parameters = new List<CommandParameterSpec> { new("@p0", 1) };

        // Act
        var spec = new DbCommandSpec("SELECT 1", parameters);
        parameters.Add(new CommandParameterSpec("@p1", 2));

        // Assert
        spec.Parameters.Should().HaveCount(1);
        spec.Parameters.Should().ContainSingle(p => p.Name == "@p0" && (int)p.Value! == 1);
    }
}
