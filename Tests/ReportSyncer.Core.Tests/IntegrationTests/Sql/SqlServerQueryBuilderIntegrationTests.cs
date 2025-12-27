using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Dependency;
using ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Sync.Contracts;
using ReportSyncer.Core.Sync.Sql;
using Xunit;

namespace ReportSyncer.Core.Tests.IntegrationTests.Sql;

[Collection("SqlQueryBuilderIntegrationTests")]
public class SqlServerQueryBuilderIntegrationTests
{
    private readonly SqlQueryBuilderIntegrationDatabaseFixture _fixture;
    private readonly SqlServerQueryBuilder _builder = new();

    public SqlServerQueryBuilderIntegrationTests(SqlQueryBuilderIntegrationDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task QueryBuilder_CountEstimate_ExecutesSuccessfully()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason);
        await using var conn = new SqlConnection(_fixture.TargetConnectionString);
        await conn.OpenAsync();

        var ctx = CreateTableExecutionContext(
            targetSchema: "rpt",
            targetTable: "HistoryEvents",
            filters: new[] { new FilterPredicate("CustomerId", FilterOperator.Equals, 50) });

        var spec = _builder.BuildCountEstimate(ctx);
        var count = await ExecuteScalarAsync<int>(conn, spec);

        count.Should().Be(1);
    }

    [SkippableFact]
    public async Task QueryBuilder_Delete_WithFilter_DeletesExpectedRows_InTransactionRollback()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason);
        await using var conn = new SqlConnection(_fixture.TargetConnectionString);
        await conn.OpenAsync();
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();

        var ctx = new DeleteCommandContext(
            "rpt",
            "HistoryEvents",
            new[] { new FilterPredicate("CustomerId", FilterOperator.Equals, 50) });

        var spec = _builder.BuildDelete(ctx);
        var rowsAffected = await ExecuteNonQueryAsync(conn, tx, spec);

        rowsAffected.Should().Be(1);

        // Confirm row gone within transaction, then rollback
        var remaining = await ExecuteScalarAsync<int>(conn, tx, _builder.BuildCountEstimate(
            CreateTableExecutionContext("rpt", "HistoryEvents", ctx.Filters)));
        remaining.Should().Be(0);
    }

    [SkippableFact]
    public async Task QueryBuilder_SelectSource_WithDateFilter_ReturnsExpectedRows()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason);
        await using var conn = new SqlConnection(_fixture.SourceConnectionString);
        await conn.OpenAsync();

        var mapping = CreateHistoryEventsMapping();
        var insertCtx = new InsertCommandContext(
            "app",
            "HistoryEvents",
            "rpt",
            "HistoryEvents",
            new[] { new FilterPredicate("EventTime", FilterOperator.GreaterOrEqual, new DateTime(2025, 12, 25)) },
            contextColumnName: "CustomerId",
            contextValue: 50,
            mapping,
            enableIdentityInsert: false,
            batchSize: 100);

        var spec = _builder.BuildSelectSource(insertCtx);
        var rows = await ExecuteReaderAsync(conn, spec);

        rows.Should().HaveCount(2);
    }

    [SkippableFact]
    public async Task QueryBuilder_InsertTarget_WithContextInjection_InsertsRow_InTransactionRollback()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason);
        await using var conn = new SqlConnection(_fixture.TargetConnectionString);
        await conn.OpenAsync();
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();

        var mapping = CreateHistoryEventsMapping();
        var insertCtx = new InsertCommandContext(
            "app",
            "HistoryEvents",
            "rpt",
            "HistoryEvents",
            Array.Empty<FilterPredicate>(),
            contextColumnName: "CustomerId",
            contextValue: 50,
            mapping,
            enableIdentityInsert: false,
            batchSize: 100);

        var spec = _builder.BuildInsertTarget(insertCtx);

        // Provide concrete values for the parameters emitted by the builder (order matches mappings)
        var parameterValues = new Queue<object?>(new object?[]
        {
            new DateTime(2025, 12, 31, 0, 0, 0),
            "ALERT",
            "payload-new",
            50
        });

        var rowsAffected = await ExecuteNonQueryAsync(conn, tx, spec, parameterValues);
        rowsAffected.Should().Be(1);

        var countSpec = _builder.BuildCountEstimate(
            CreateTableExecutionContext(
                "rpt",
                "HistoryEvents",
                new[] { new FilterPredicate("CustomerId", FilterOperator.Equals, 50) }));

        var countAfter = await ExecuteScalarAsync<int>(conn, tx, countSpec);
        countAfter.Should().Be(2); // original 1 + inserted 1
    }

    private static TableExecutionContext CreateTableExecutionContext(string targetSchema, string targetTable, IReadOnlyList<FilterPredicate> filters)
    {
        return new TableExecutionContext(
            Guid.NewGuid(),
            "JobIT",
            "Src",
            "Dst",
            DatabaseType.SqlServer,
            DatabaseType.SqlServer,
            targetSchema, // source schema placeholder not used in count/delete
            targetTable,
            targetSchema,
            targetTable,
            dryRun: false,
            preSyncTargetDelete: false,
            enableIdentityInsert: false,
            contextColumnName: null,
            contextValue: null,
            filters,
            new TableMapping(
                TableIdentifier.Parse($"{targetSchema}.{targetTable}"),
                TableIdentifier.Parse($"{targetSchema}.{targetTable}"),
                Array.Empty<ColumnMapping>(),
                HasWarnings: false),
            ExecutionPlan.Empty,
            batchSize: 1000);
    }

    private static TableMapping CreateHistoryEventsMapping()
    {
        var srcTime = new ColumnSchema("EventTime", typeof(DateTime), "datetime2", false, false, false, null);
        var srcType = new ColumnSchema("EventType", typeof(string), "nvarchar(40)", false, false, false, 40);
        var srcPayload = new ColumnSchema("Payload", typeof(string), "nvarchar(200)", true, false, false, 200);

        var tgtTime = new ColumnSchema("EventTime", typeof(DateTime), "datetime2", false, false, false, null);
        var tgtType = new ColumnSchema("EventType", typeof(string), "nvarchar(40)", false, false, false, 40);
        var tgtPayload = new ColumnSchema("Payload", typeof(string), "nvarchar(200)", true, false, false, 200);
        var tgtCustomer = new ColumnSchema("CustomerId", typeof(int), "int", false, false, true, null);

        return new TableMapping(
            TableIdentifier.Parse("app.HistoryEvents"),
            TableIdentifier.Parse("rpt.HistoryEvents"),
            new[]
            {
                new ColumnMapping(srcTime, tgtTime, MappingKind.OneToOne, null),
                new ColumnMapping(srcType, tgtType, MappingKind.OneToOne, null),
                new ColumnMapping(srcPayload, tgtPayload, MappingKind.OneToOne, null),
                new ColumnMapping(null, tgtCustomer, MappingKind.ContextColumn, null)
            },
            HasWarnings: false);
    }

    private static async Task<int> ExecuteNonQueryAsync(SqlConnection conn, SqlTransaction tx, DbCommandSpec spec, Queue<object?>? overrideValues = null)
    {
        await using var cmd = new SqlCommand(spec.Sql, conn, tx);
        AddParameters(cmd, spec.Parameters, overrideValues);
        return await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<T> ExecuteScalarAsync<T>(SqlConnection conn, DbCommandSpec spec)
    {
        await using var cmd = new SqlCommand(spec.Sql, conn);
        AddParameters(cmd, spec.Parameters, null);
        var result = await cmd.ExecuteScalarAsync();
        return result is null ? default! : (T)Convert.ChangeType(result, typeof(T));
    }

    private static async Task<T> ExecuteScalarAsync<T>(SqlConnection conn, SqlTransaction tx, DbCommandSpec spec)
    {
        await using var cmd = new SqlCommand(spec.Sql, conn, tx);
        AddParameters(cmd, spec.Parameters, null);
        var result = await cmd.ExecuteScalarAsync();
        return result is null ? default! : (T)Convert.ChangeType(result, typeof(T));
    }

    private static async Task<List<Dictionary<string, object?>>> ExecuteReaderAsync(SqlConnection conn, DbCommandSpec spec)
    {
        await using var cmd = new SqlCommand(spec.Sql, conn);
        AddParameters(cmd, spec.Parameters, null);
        await using var reader = await cmd.ExecuteReaderAsync();

        var results = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
            }

            results.Add(row);
        }

        return results;
    }

    private static void AddParameters(SqlCommand cmd, IReadOnlyList<CommandParameterSpec> parameters, Queue<object?>? overrides)
    {
        foreach (var p in parameters)
        {
            var value = overrides is { Count: > 0 } ? overrides.Dequeue() : p.Value;
            var sqlParam = cmd.Parameters.AddWithValue(p.Name, value ?? DBNull.Value);
            if (p.DbType.HasValue)
            {
                sqlParam.DbType = p.DbType.Value;
            }
        }
    }
}
