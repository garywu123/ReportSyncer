using System.Collections;
using System.Data;
using System.Data.Common;
using FluentAssertions;
using Moq;
using DotNetToolkit.Database.Abstractions;
using ReportSyncer.Core.Observability;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Dependency;
using ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Sync.Contracts;
using ReportSyncer.Core.Sync.Sql;

namespace ReportSyncer.Core.Tests.Observability;

public class WorkEstimatorTests
{
    [Fact]
    public async Task EstimateAsync_PreSyncTargetDeleteFalse_ReturnsInsertOnly()
    {
        // Arrange
        var sourceFactory = new FakeDbConnectionFactory(10);
        var targetFactory = new FakeDbConnectionFactory();
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
        var sourceFactory = new FakeDbConnectionFactory(5);
        var targetFactory = new FakeDbConnectionFactory(4, 8); // delete rows, total rows
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
        var sourceFactory = new FakeDbConnectionFactory(1);
        var targetFactory = new FakeDbConnectionFactory(2, 4);

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
        var targetFactory = new FakeDbConnectionFactory();
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

    #region Fakes

    private sealed class FakeDbConnectionFactory : IDbConnectionFactory
    {
        private readonly Queue<long> _results;

        public FakeDbConnectionFactory(params long[] results)
        {
            _results = new Queue<long>(results);
        }

        public IDbConnection CreateConnection() => new FakeDbConnection(_results);

        public Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IDbConnection>(new FakeDbConnection(_results));
        }

        public DbProviderFactory GetProviderFactory() => throw new NotImplementedException();
    }

    private sealed class ThrowingDbConnectionFactory : IDbConnectionFactory
    {
        private readonly Exception _ex;
        public ThrowingDbConnectionFactory(Exception ex) => _ex = ex;
        public IDbConnection CreateConnection() => throw _ex;
        public Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default) => Task.FromException<IDbConnection>(_ex);
        public DbProviderFactory GetProviderFactory() => throw new NotImplementedException();
    }

    private sealed class FakeDbConnection : DbConnection
    {
        private readonly Queue<long> _results;
        public List<FakeDbCommand> Commands { get; } = new();

        public FakeDbConnection(Queue<long> results) => _results = results;

        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "Fake";
        public override string DataSource => "Fake";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => ConnectionState.Open;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotImplementedException();
        protected override DbCommand CreateDbCommand()
        {
            var cmd = new FakeDbCommand(_results);
            Commands.Add(cmd);
            return cmd;
        }
    }

    private sealed class FakeDbCommand : DbCommand
    {
        private readonly Queue<long> _results;
        public FakeDbCommand(Queue<long> results) => _results = results;

        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; } = CommandType.Text;
        protected override DbConnection DbConnection { get; set; } = null!;
        protected override DbParameterCollection DbParameterCollection { get; } = new FakeParameterCollection();
        protected override DbTransaction DbTransaction { get; set; } = null!;
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }

        public override void Cancel() { }
        public override int ExecuteNonQuery() => throw new NotImplementedException();
        public override object ExecuteScalar() => _results.Count > 0 ? _results.Dequeue() : 0;
        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
        public override Task<object> ExecuteScalarAsync(CancellationToken cancellationToken) => Task.FromResult<object>(ExecuteScalar());
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new FakeDbParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotImplementedException();
    }

    private sealed class FakeParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _inner = new();

        public override int Count => _inner.Count;
        public override object SyncRoot => this;
        public override int Add(object value) { _inner.Add((DbParameter)value); return _inner.Count - 1; }
        public override void AddRange(Array values) { foreach (var v in values) Add(v!); }
        public override void Clear() => _inner.Clear();
        public override bool Contains(object value) => _inner.Contains((DbParameter)value);
        public override bool Contains(string value) => _inner.Any(p => p.ParameterName == value);
        public override void CopyTo(Array array, int index) => _inner.ToArray().CopyTo(array, index);
        public override IEnumerator GetEnumerator() => _inner.GetEnumerator();
        public override int IndexOf(object value) => _inner.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName) => _inner.FindIndex(p => p.ParameterName == parameterName);
        public override void Insert(int index, object value) => _inner.Insert(index, (DbParameter)value);
        public override void Remove(object value) => _inner.Remove((DbParameter)value);
        public override void RemoveAt(int index) => _inner.RemoveAt(index);
        public override void RemoveAt(string parameterName)
        {
            var idx = IndexOf(parameterName);
            if (idx >= 0) _inner.RemoveAt(idx);
        }
        protected override DbParameter GetParameter(int index) => _inner[index];
        protected override DbParameter GetParameter(string parameterName) => _inner[IndexOf(parameterName)];
        protected override void SetParameter(int index, DbParameter value) => _inner[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value)
        {
            var idx = IndexOf(parameterName);
            if (idx >= 0) _inner[idx] = value;
            else _inner.Add(value);
        }
    }

    private sealed class FakeDbParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
        public override bool IsNullable { get; set; }
        public override string ParameterName { get; set; } = string.Empty;
        public override int Size { get; set; }
        public override string SourceColumn { get; set; } = string.Empty;
        public override bool SourceColumnNullMapping { get; set; }
        public override object? Value { get; set; }
        public override void ResetDbType() { }
    }

    #endregion
}
