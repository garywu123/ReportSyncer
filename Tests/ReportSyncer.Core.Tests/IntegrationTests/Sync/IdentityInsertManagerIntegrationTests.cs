using FluentAssertions;
using Microsoft.Data.SqlClient;
using DotNetToolkit.Database.Abstractions;
using System.Data;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Dependency;
using ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Sync;
using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Core.Tests.IntegrationTests.Sync;

[Collection("SqlDataWriterIntegrationTests")]
public class IdentityInsertManagerIntegrationTests
{
    private readonly SqlDataWriterIntegrationFixture _fixture;

    public IdentityInsertManagerIntegrationTests(SqlDataWriterIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task IdentityInsertScope_AllowsExplicitIdentity_ThenDisallowsAfterDispose()
    {
        Skip.If(!string.IsNullOrWhiteSpace(_fixture.SkipReason), _fixture.SkipReason);
        await _fixture.ResetTableAsync();

        if (_fixture.ConnectionString is null)
            throw new InvalidOperationException("Fixture not initialized.");

        var manager = new IdentityInsertManager(_ => new PersistentDbContext(_fixture.ConnectionString));
        var ctx = CreateContext(enableIdentityInsert: true);

        using (var db = new PersistentDbContext(_fixture.ConnectionString))
        {
            await using (await manager.BeginAsync(ctx, CancellationToken.None, db))
            {
                var cmdOn = db.CreateCommand("INSERT INTO rpt.TargetEvents(CustomerId, EventId, EventTime, Payload) VALUES (50, 500, GETDATE(), 'on');", System.Data.CommandType.Text);
                var inserted = await db.ExecuteNonQueryAsync(cmdOn, CancellationToken.None);
                inserted.Should().Be(1);
            }
        }

        using (var db = new PersistentDbContext(_fixture.ConnectionString))
        {
            var cmdOff = db.CreateCommand("INSERT INTO rpt.TargetEvents(CustomerId, EventId, EventTime, Payload) VALUES (51, 501, GETDATE(), 'off');", System.Data.CommandType.Text);
            var act = () => db.ExecuteNonQueryAsync(cmdOff, CancellationToken.None);
            await act.Should().ThrowAsync<SqlException>();
        }
    }

    private static TableExecutionContext CreateContext(bool enableIdentityInsert)
    {
        var src = new ColumnSchema("EventTime", typeof(DateTime), "datetime2", false, false, false, null);
        var tgt = new ColumnSchema("EventTime", typeof(DateTime), "datetime2", false, false, false, null);
        var tgtIdentity = new ColumnSchema("EventId", typeof(int), "int", false, true, true, null);
        var tgtCustomer = new ColumnSchema("CustomerId", typeof(int), "int", false, false, true, null);

        var mapping = new TableMapping(
            TableIdentifier.Parse("app.SourceEvents"),
            TableIdentifier.Parse("rpt.TargetEvents"),
            new[]
            {
                new ColumnMapping(src, tgt, MappingKind.OneToOne, null),
                new ColumnMapping(null, tgtCustomer, MappingKind.ContextColumn, null),
                new ColumnMapping(tgtIdentity, tgtIdentity, MappingKind.OneToOne, null)
            },
            HasWarnings: false);

        return new TableExecutionContext(
            Guid.NewGuid(),
            "IdentityInsertIT",
            sourceConnectionName: "Target",
            targetConnectionName: "Target",
            sourceDbType: DatabaseType.SqlServer,
            targetDbType: DatabaseType.SqlServer,
            sourceSchema: "app",
            sourceTable: "SourceEvents",
            targetSchema: "rpt",
            targetTable: "TargetEvents",
            dryRun: false,
            preSyncTargetDelete: false,
            enableIdentityInsert: enableIdentityInsert,
            contextColumnName: "CustomerId",
            contextValue: 1,
            filters: Array.Empty<FilterPredicate>(),
            tableMapping: mapping,
            executionPlan: ExecutionPlan.Empty,
            batchSize: 500);
    }

    private sealed class PersistentDbContext : IDbContext
    {
        private readonly SqlConnection _conn;

        public PersistentDbContext(string connectionString)
        {
            _conn = new SqlConnection(connectionString);
            _conn.Open();
        }

        public IDbCommandWrapper CreateCommand(string storedProcedureName)
            => CreateCommand(storedProcedureName, CommandType.StoredProcedure);

        public IDbCommandWrapper CreateCommand(string commandText, CommandType commandType)
            => new SimpleCommandWrapper(_conn, commandText, commandType);

        public Task<int> ExecuteNonQueryAsync(IDbCommandWrapper command, CancellationToken cancellationToken = default)
        {
            var wrapper = (SimpleCommandWrapper)command;
            wrapper.Command.Connection = _conn;
            return wrapper.Command.ExecuteNonQueryAsync(cancellationToken);
        }

        public Task<List<T>> ExecuteQueryAsync<T>(IDbCommandWrapper command, CancellationToken cancellationToken = default) where T : new()
            => Task.FromResult(new List<T>());

        public void Dispose()
        {
            _conn.Dispose();
        }
    }

    private sealed class SimpleCommandWrapper : IDbCommandWrapper
    {
        public SqlCommand Command { get; }

        public SimpleCommandWrapper(SqlConnection conn, string text, CommandType type)
        {
            Command = conn.CreateCommand();
            Command.CommandText = text;
            Command.CommandType = type;
        }

        public void AddParameter(string name, object value, DbType type, ParameterDirection direction = ParameterDirection.Input)
        {
            var p = Command.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            p.DbType = type;
            p.Direction = direction;
            Command.Parameters.Add(p);
        }

        public T GetParameterValue<T>(string name) => default!;

        public string CommandText => Command.CommandText;

        public CommandType CommandType => Command.CommandType;

        public IEnumerable<IDbDataParameter> Parameters => Command.Parameters.Cast<IDbDataParameter>();
    }
}
