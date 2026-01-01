using System;
using System.Threading.Tasks;
using DotNetToolkit.Database.Configuration;
using DotNetToolkit.Database.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
using ReportSyncer.Core.Observability;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Dependency;
using ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Sync.Contracts;
using ReportSyncer.Core.Sync.Sql;
using Xunit;

namespace ReportSyncer.Core.Tests.IntegrationTests.Observability;

[Collection("WorkEstimatorIntegrationTests")]
public class WorkEstimatorIntegrationTests
{
    private readonly WorkEstimatorIntegrationDatabaseFixture _fixture;

    public WorkEstimatorIntegrationTests(WorkEstimatorIntegrationDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task WorkEstimator_CountsSourceRows_WithDateFilter()
    {
        Skip.If(!string.IsNullOrWhiteSpace(_fixture.SkipReason), _fixture.SkipReason);
        var (sourceFactory, targetFactory) = CreateFactories();
        var estimator = new WorkEstimator(sourceFactory, targetFactory, new SqlServerQueryBuilder());
        var ctx = CreateContext(
            preSyncTargetDelete: false,
            filters: new[] { new FilterPredicate("EventTime", FilterOperator.GreaterOrEqual, new DateTime(2025, 12, 25)) });

        var estimate = await estimator.EstimateAsync(ctx, CancellationToken.None);

        estimate.EstimatedRowsToInsert.Should().Be(2);
        estimate.EstimatedRowsToDelete.Should().Be(0);
    }

    [SkippableFact]
    public async Task WorkEstimator_CountsTargetDeleteRows_WithContextCustomerId()
    {
        Skip.If(!string.IsNullOrWhiteSpace(_fixture.SkipReason), _fixture.SkipReason);
        var (sourceFactory, targetFactory) = CreateFactories();
        var estimator = new WorkEstimator(sourceFactory, targetFactory, new SqlServerQueryBuilder());
        var ctx = CreateContext(preSyncTargetDelete: true, contextColumn: "CustomerId", contextValue: 50);

        var estimate = await estimator.EstimateAsync(ctx, CancellationToken.None);

        estimate.EstimatedRowsToDelete.Should().Be(3);
        estimate.DeleteStats!.TargetTotalRows.Should().Be(5);
    }

    [SkippableFact]
    public async Task WorkEstimator_ComputesDeletePct_AgainstTargetTotal()
    {
        Skip.If(!string.IsNullOrWhiteSpace(_fixture.SkipReason), _fixture.SkipReason);
        var (sourceFactory, targetFactory) = CreateFactories();
        var estimator = new WorkEstimator(sourceFactory, targetFactory, new SqlServerQueryBuilder());
        var ctx = CreateContext(preSyncTargetDelete: true, contextColumn: "CustomerId", contextValue: 50);

        var estimate = await estimator.EstimateAsync(ctx, CancellationToken.None);

        estimate.EstimatedDeletePct.Should().BeApproximately(60.0, 0.001);
    }

    private (DbConnectionFactory Source, DbConnectionFactory Target) CreateFactories()
    {
        if (_fixture.ConnectionString is null)
            throw new InvalidOperationException("Fixture not initialized.");

        var options = Options.Create(new DatabaseSettings
        {
            ProviderName = "Microsoft.Data.SqlClient",
            ConnectionString = _fixture.ConnectionString
        });

        var source = new DbConnectionFactory(options);
        var target = new DbConnectionFactory(options);
        return (source, target);
    }

    private static TableExecutionContext CreateContext(
        bool preSyncTargetDelete,
        FilterPredicate[]? filters = null,
        string? contextColumn = null,
        object? contextValue = null)
    {
        var srcEventTime = new ColumnSchema("EventTime", typeof(DateTime), "datetime2", false, false, false, null);
        var tgtEventTime = new ColumnSchema("EventTime", typeof(DateTime), "datetime2", false, false, false, null);
        var srcPayload = new ColumnSchema("Payload", typeof(string), "nvarchar(100)", true, false, false, 100);
        var tgtPayload = new ColumnSchema("Payload", typeof(string), "nvarchar(100)", true, false, false, 100);
        var tgtCustomer = new ColumnSchema("CustomerId", typeof(int), "int", false, false, true, null);

        var mapping = new TableMapping(
            TableIdentifier.Parse("app.SourceEvents"),
            TableIdentifier.Parse("rpt.TargetEvents"),
            new[]
            {
                new ColumnMapping(srcEventTime, tgtEventTime, MappingKind.OneToOne, null),
                new ColumnMapping(srcPayload, tgtPayload, MappingKind.OneToOne, null),
                new ColumnMapping(null, tgtCustomer, MappingKind.ContextColumn, null)
            },
            HasWarnings: false);

        return new TableExecutionContext(
            Guid.NewGuid(),
            "Job-IT",
            "SourceConn",
            "TargetConn",
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
            filters: filters ?? Array.Empty<FilterPredicate>(),
            mapping,
            ExecutionPlan.Empty,
            batchSize: 500, etaSmoothing: null);
    }
}
