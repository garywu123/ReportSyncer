using System.Data;
using System.Data.Common;
using System.Collections;
using DotNetToolkit.Database.Abstractions;
using FluentAssertions;
using Moq;
using ReportSyncer.Core.Exceptions;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Dependency;
using ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Sync;
using ReportSyncer.Core.Sync.Contracts;
using ReportSyncer.Core.Sync.Sql;

namespace ReportSyncer.Core.Tests.UnitTests.Sync;

#pragma warning disable CS8765
public class TableRunnerTests
{
    [Fact]
    public async Task RunAsync_DryRun_SkipsWriterAndReturnsDryResult()
    {
        var writer = new Mock<IDataWriter>(MockBehavior.Strict);
        var runner = new TableRunner(new ThrowingConnectionFactory(), writer.Object, new SqlServerQueryBuilder());
        var ctx = CreateContext(dryRun: true, preSyncDelete: true, enableIdentityInsert: false);

        var result = await runner.RunAsync(ctx, CancellationToken.None);

        result.Status.Should().Be(TableStatus.SkippedDryRun);
        result.RowsDeleted.Should().Be(0);
        result.RowsInserted.Should().Be(0);
        writer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RunAsync_PreSyncDelete_DeletesThenInsertsMappedRows()
    {
        var sourceRows = new List<Dictionary<string, object?>>
        {
            new() { ["EventTime"] = new DateTime(2025, 12, 24), ["EventId"] = 10 },
            new() { ["EventTime"] = new DateTime(2025, 12, 25), ["EventId"] = 11 }
        };

        var writer = new Mock<IDataWriter>(MockBehavior.Strict);
        IReadOnlyList<IReadOnlyDictionary<string, object?>>? capturedRows = null;

        writer.Setup(w => w.DeleteAsync(It.IsAny<TableExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        writer.Setup(w => w.InsertAsync(It.IsAny<TableExecutionContext>(), It.IsAny<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(), It.IsAny<CancellationToken>()))
            .Callback<TableExecutionContext, IReadOnlyList<IReadOnlyDictionary<string, object?>>, CancellationToken>((_, rows, _) => capturedRows = rows)
            .ReturnsAsync(2);

        var runner = new TableRunner(new InMemoryRowConnectionFactory(sourceRows), writer.Object, new SqlServerQueryBuilder());
        var ctx = CreateContext(dryRun: false, preSyncDelete: true, enableIdentityInsert: true);

        var result = await runner.RunAsync(ctx, CancellationToken.None);

        result.Status.Should().Be(TableStatus.Succeeded);
        result.RowsDeleted.Should().Be(1);
        result.RowsInserted.Should().Be(2);

        writer.Verify(w => w.DeleteAsync(ctx, It.IsAny<CancellationToken>()), Times.Once);
        writer.Verify(w => w.InsertAsync(ctx, It.IsAny<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(), It.IsAny<CancellationToken>()), Times.Once);
        capturedRows.Should().NotBeNull();
        capturedRows!.Should().HaveCount(2);
        capturedRows![0].Should().Contain(new KeyValuePair<string, object?>("CustomerId", 7));
        capturedRows![0].Should().Contain(new KeyValuePair<string, object?>("Payload", "const"));
        capturedRows![0].Should().ContainKey("EventId");
        capturedRows![0]["EventTime"].Should().Be(sourceRows[0]["EventTime"]);
    }

    [Fact]
    public async Task RunAsync_WhenPreSyncDeleteFalse_SkipsDelete()
    {
        var sourceRows = new List<Dictionary<string, object?>>
        {
            new() { ["EventTime"] = new DateTime(2025, 12, 24), ["EventId"] = 10 }
        };

        var writer = new Mock<IDataWriter>(MockBehavior.Strict);
        writer.Setup(w => w.InsertAsync(It.IsAny<TableExecutionContext>(), It.IsAny<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var runner = new TableRunner(new InMemoryRowConnectionFactory(sourceRows), writer.Object, new SqlServerQueryBuilder());
        var ctx = CreateContext(dryRun: false, preSyncDelete: false, enableIdentityInsert: false);

        var result = await runner.RunAsync(ctx, CancellationToken.None);

        result.RowsDeleted.Should().Be(0);
        writer.Verify(w => w.DeleteAsync(It.IsAny<TableExecutionContext>(), It.IsAny<CancellationToken>()), Times.Never);
        writer.Verify(w => w.InsertAsync(ctx, It.IsAny<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenWriterThrows_WrapsWithTableContext()
    {
        var sourceRows = new List<Dictionary<string, object?>>
        {
            new() { ["EventTime"] = new DateTime(2025, 12, 24), ["EventId"] = 10 }
        };

        var writer = new Mock<IDataWriter>(MockBehavior.Strict);
        writer.Setup(w => w.InsertAsync(It.IsAny<TableExecutionContext>(), It.IsAny<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SyncExecutionException("boom"));

        var runner = new TableRunner(new InMemoryRowConnectionFactory(sourceRows), writer.Object, new SqlServerQueryBuilder());
        var ctx = CreateContext(dryRun: false, preSyncDelete: false, enableIdentityInsert: false);

        var act = () => runner.RunAsync(ctx, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<SyncExecutionException>();
        ex.Which.Message.Should().Contain("Insert").And.Contain(ctx.TargetTable);
    }

    private static TableExecutionContext CreateContext(bool dryRun, bool preSyncDelete, bool enableIdentityInsert)
    {
        var srcEventTime = new ColumnSchema("EventTime", typeof(DateTime), "datetime2", false, false, false, null);
        var tgtEventTime = new ColumnSchema("EventTime", typeof(DateTime), "datetime2", false, false, false, null);
        var tgtPayload = new ColumnSchema("Payload", typeof(string), "nvarchar(100)", true, false, false, 100);
        var tgtCustomer = new ColumnSchema("CustomerId", typeof(int), "int", false, false, true, null);
        var tgtIdentity = new ColumnSchema("EventId", typeof(int), "int", false, true, true, null);

        var mapping = new TableMapping(
            TableIdentifier.Parse("src.SourceEvents"),
            TableIdentifier.Parse("rpt.TargetEvents"),
            new[]
            {
                new ColumnMapping(srcEventTime, tgtEventTime, MappingKind.OneToOne, null),
                new ColumnMapping(null, tgtPayload, MappingKind.Constant, "const"),
                new ColumnMapping(null, tgtCustomer, MappingKind.ContextColumn, null),
                new ColumnMapping(tgtIdentity, tgtIdentity, MappingKind.OneToOne, null)
            },
            HasWarnings: false);

        return new TableExecutionContext(
            Guid.NewGuid(),
            "Job1",
            "Src",
            "Dst",
            DatabaseType.SqlServer,
            DatabaseType.SqlServer,
            "src",
            "SourceEvents",
            "rpt",
            "TargetEvents",
            dryRun: dryRun,
            preSyncTargetDelete: preSyncDelete,
            enableIdentityInsert: enableIdentityInsert,
            contextColumnName: "CustomerId",
            contextValue: 7,
            filters: new[] { new FilterPredicate("CustomerId", FilterOperator.Equals, 7) },
            mapping,
            ExecutionPlan.Empty,
            batchSize: 1000);
    }

    private sealed class ThrowingConnectionFactory : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => throw new InvalidOperationException("Should not be used in dry-run.");
        public Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
            => Task.FromException<IDbConnection>(new InvalidOperationException("Should not be used in dry-run."));
        public DbProviderFactory GetProviderFactory() => throw new NotImplementedException();
    }

    private sealed class InMemoryRowConnectionFactory : IDbConnectionFactory
    {
        private readonly List<Dictionary<string, object?>> _rows;

        public InMemoryRowConnectionFactory(List<Dictionary<string, object?>> rows)
        {
            _rows = rows;
        }

        public IDbConnection CreateConnection() => new InMemoryRowConnection(_rows);

        public Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IDbConnection>(new InMemoryRowConnection(_rows));

        public DbProviderFactory GetProviderFactory() => throw new NotImplementedException();
    }

    private sealed class InMemoryRowConnection : DbConnection
    {
        private readonly List<Dictionary<string, object?>> _rows;

        public InMemoryRowConnection(List<Dictionary<string, object?>> rows)
        {
            _rows = rows;
        }

        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "InMemory";
        public override string DataSource => "InMemory";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => ConnectionState.Open;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotImplementedException();
        protected override DbCommand CreateDbCommand() => new InMemoryRowCommand(_rows);
    }

    private sealed class InMemoryRowCommand : DbCommand
    {
        private readonly List<Dictionary<string, object?>> _rows;
        private readonly InMemoryParameterCollection _parameters = new();

        public InMemoryRowCommand(List<Dictionary<string, object?>> rows)
        {
            _rows = rows;
        }

        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; } = 30;
        public override CommandType CommandType { get; set; } = CommandType.Text;
        protected override DbConnection DbConnection { get; set; } = null!;
        protected override DbParameterCollection DbParameterCollection => _parameters;
        protected override DbTransaction DbTransaction { get; set; } = null!;
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }

        public override void Cancel() { }
        public override int ExecuteNonQuery() => throw new NotImplementedException();
        public override object? ExecuteScalar() => throw new NotImplementedException();
        public override void Prepare() { }

        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) => Task.FromResult(0);

        public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken) => Task.FromResult<object?>(null);

        protected override DbParameter CreateDbParameter() => new InMemoryParameter();

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            var table = new DataTable();
            if (_rows.Count > 0)
            {
                foreach (var column in _rows[0].Keys)
                {
                    table.Columns.Add(column, typeof(object));
                }

                foreach (var row in _rows)
                {
                    var values = new object?[table.Columns.Count];
                    for (var i = 0; i < table.Columns.Count; i++)
                    {
                        var name = table.Columns[i].ColumnName;
                        values[i] = row.TryGetValue(name, out var v) ? v ?? DBNull.Value : DBNull.Value;
                    }
                    table.Rows.Add(values);
                }
            }

            return table.CreateDataReader();
        }

    }

    #pragma warning disable CS8765
    private sealed class InMemoryParameterCollection : DbParameterCollection
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
        public override void RemoveAt(string? parameterName)
        {
            if (parameterName is null) return;
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

    private sealed class InMemoryParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
        public override bool IsNullable { get; set; }
        public override string ParameterName { get; set; } = string.Empty;
        public override string SourceColumn { get; set; } = string.Empty;
        public override bool SourceColumnNullMapping { get; set; }
        public override object? Value { get; set; }
        public override int Size { get; set; }
        public override void ResetDbType() { }
    }
#pragma warning restore CS8765
}
