using System.Collections;
using System.Data.Common;
using FluentAssertions;
using Moq;
using ReportSyncer.Core.Observability;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Dependency;
using ReportSyncer.Core.Schema.Mapping;
using ReportSyncer.Core.Sync;
using ReportSyncer.Core.Sync.Contracts;
using ReportSyncer.Core.Tests.Helpers.Observability;

namespace ReportSyncer.Core.Tests.UnitTests.Sync;

public class TableRunnerProgressEventsTests
{
    [Fact]
    public async Task RunPhaseAsync_DeleteSkip_EmitsStartedAndSkipped()
    {
        var reporter = new InMemoryProgressReporter();
        var runner = CreateRunner(reporter, writer: new Mock<IDataWriter>(MockBehavior.Strict).Object);
        var ctx = CreateContext(preSyncDelete: false);

        var result = await runner.RunPhaseAsync(ctx, SyncPhase.Delete, CancellationToken.None);

        result.RowsDeleted.Should().Be(0);
        reporter.TableEvents.Should().HaveCount(2);
        reporter.TableEvents[0].Kind.Should().Be(ProgressEventKind.Started);
        reporter.TableEvents[1].Kind.Should().Be(ProgressEventKind.Skipped);
    }

    [Fact]
    public async Task RunPhaseAsync_InsertSuccess_EmitsStartedAndCompleted()
    {
        var reporter = new InMemoryProgressReporter();
        var writer = new Mock<IDataWriter>(MockBehavior.Strict);
        writer.Setup(w => w.InsertAsync(It.IsAny<TableExecutionContext>(), It.IsAny<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(), It.IsAny<IProgress<InsertBatchProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        var runner = CreateRunner(reporter, writer.Object, rows: new List<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["Id"] = 1 },
            new Dictionary<string, object?> { ["Id"] = 2 }
        });
        var ctx = CreateContext(preSyncDelete: true);

        var result = await runner.RunPhaseAsync(ctx, SyncPhase.Insert, CancellationToken.None);

        result.RowsInserted.Should().Be(2);
        reporter.TableEvents.Should().HaveCount(2);
        reporter.TableEvents[0].Kind.Should().Be(ProgressEventKind.Started);
        reporter.TableEvents[1].Kind.Should().Be(ProgressEventKind.Completed);
        reporter.TableEvents[1].RowsAffected.Should().Be(2);
    }

    [Fact]
    public async Task RunPhaseAsync_InsertFailure_EmitsFailed()
    {
        var reporter = new InMemoryProgressReporter();
        var writer = new Mock<IDataWriter>(MockBehavior.Strict);
        writer.Setup(w => w.InsertAsync(It.IsAny<TableExecutionContext>(), It.IsAny<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(), It.IsAny<IProgress<InsertBatchProgress>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var runner = CreateRunner(reporter, writer.Object, rows: new List<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["Id"] = 1 }
        });
        var ctx = CreateContext(preSyncDelete: true);

        var act = () => runner.RunPhaseAsync(ctx, SyncPhase.Insert, CancellationToken.None);

        await act.Should().ThrowAsync<Exception>();
        reporter.TableEvents.Should().Contain(e => e.Kind == ProgressEventKind.Failed && e.Phase == SyncPhase.Insert);
    }

    private static TableRunner CreateRunner(IJobProgressReporter reporter, IDataWriter writer, List<IReadOnlyDictionary<string, object?>>? rows = null)
    {
        rows ??= new List<IReadOnlyDictionary<string, object?>>();
        var sourceFactory = new InMemoryRowConnectionFactory(rows);
        var builder = new ReportSyncer.Core.Sync.Sql.SqlServerQueryBuilder();
        return new TableRunner(sourceFactory, writer, builder, reporter);
    }

    private static TableExecutionContext CreateContext(bool preSyncDelete)
    {
        var col = new ColumnSchema("Id", typeof(int), "int", false, false, true, null);
        var mapping = new TableMapping(
            TableIdentifier.Parse("src.T"),
            TableIdentifier.Parse("dbo.T"),
            new[] { new ColumnMapping(col, col, MappingKind.OneToOne, null) },
            HasWarnings: false);

        return new TableExecutionContext(
            Guid.NewGuid(),
            "Job1",
            "Src",
            "Dst",
            DatabaseType.SqlServer,
            DatabaseType.SqlServer,
            "src",
            "T",
            "dbo",
            "T",
            dryRun: false,
            preSyncTargetDelete: preSyncDelete,
            enableIdentityInsert: false,
            contextColumnName: null,
            contextValue: null,
            filters: Array.Empty<FilterPredicate>(),
            mapping,
            ExecutionPlan.Empty,
            batchSize: 1000, etaSmoothing: null);
    }

    private class InMemoryRowConnectionFactory : DotNetToolkit.Database.Abstractions.IDbConnectionFactory
    {
        private readonly List<IReadOnlyDictionary<string, object?>> _rows;

        public InMemoryRowConnectionFactory(List<IReadOnlyDictionary<string, object?>> rows)
        {
            _rows = rows;
        }

        public System.Data.IDbConnection CreateConnection() => new InMemoryRowConnection(_rows);

        public Task<System.Data.IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<System.Data.IDbConnection>(new InMemoryRowConnection(_rows));

        public DbProviderFactory GetProviderFactory() => throw new NotImplementedException();
    }

    private class InMemoryRowConnection : DbConnection
    {
        private readonly List<IReadOnlyDictionary<string, object?>> _rows;

        public InMemoryRowConnection(List<IReadOnlyDictionary<string, object?>> rows)
        {
            _rows = rows;
        }

#pragma warning disable CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).
        public override string ConnectionString { get; set; } = string.Empty;
#pragma warning restore CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).
        public override string Database => "InMemory";
        public override string DataSource => "InMemory";
        public override string ServerVersion => "1.0";
        public override System.Data.ConnectionState State => System.Data.ConnectionState.Open;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }

        protected override DbTransaction BeginDbTransaction(System.Data.IsolationLevel isolationLevel) => throw new NotImplementedException();
        protected override DbCommand CreateDbCommand() => new InMemoryRowCommand(_rows);
    }

    private class InMemoryRowCommand : DbCommand
    {
        private readonly List<IReadOnlyDictionary<string, object?>> _rows;

        public InMemoryRowCommand(List<IReadOnlyDictionary<string, object?>> rows)
        {
            _rows = rows;
        }


#pragma warning disable CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).
        public override string CommandText { get; set; } = string.Empty;
#pragma warning restore CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).
        public override int CommandTimeout { get; set; }
        public override System.Data.CommandType CommandType { get; set; } = System.Data.CommandType.Text;
#pragma warning disable CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).
        protected override DbConnection DbConnection { get; set; } = null!;
#pragma warning restore CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).
        protected override DbParameterCollection DbParameterCollection { get; } = new InMemoryParameterCollection();
#pragma warning disable CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).
        protected override DbTransaction DbTransaction { get; set; } = null!;
#pragma warning restore CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).
        public override bool DesignTimeVisible { get; set; }
        public override System.Data.UpdateRowSource UpdatedRowSource { get; set; }

        public override void Cancel() { }
        public override int ExecuteNonQuery() => 0;
        public override object? ExecuteScalar() => null;
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new InMemoryParameter();
        protected override DbDataReader ExecuteDbDataReader(System.Data.CommandBehavior behavior) => new InMemoryRowReader(_rows);
    }

    private class InMemoryRowReader : DbDataReader
    {
        private readonly System.Data.DataTableReader _inner;

        public InMemoryRowReader(List<IReadOnlyDictionary<string, object?>> rows)
        {
            var table = new System.Data.DataTable();
            if (rows.Count > 0)
            {
                foreach (var col in rows[0].Keys)
                {
                    table.Columns.Add(col, typeof(object));
                }

                foreach (var row in rows)
                {
                    var values = new object?[table.Columns.Count];
                    for (var i = 0; i < table.Columns.Count; i++)
                    {
                        var name = table.Columns[i].ColumnName;
                        values[i] = row.TryGetValue(name, out var val) ? val ?? DBNull.Value : DBNull.Value;
                    }
                    table.Rows.Add(values);
                }
            }

            _inner = table.CreateDataReader();
        }

        public override object this[int ordinal] => _inner[ordinal];
        public override object this[string name] => _inner[name];
        public override int Depth => _inner.Depth;
        public override int FieldCount => _inner.FieldCount;
        public override bool HasRows => _inner.HasRows;
        public override bool IsClosed => _inner.IsClosed;
        public override int RecordsAffected => _inner.RecordsAffected;
        public override bool GetBoolean(int ordinal) => _inner.GetBoolean(ordinal);
        public override byte GetByte(int ordinal) => _inner.GetByte(ordinal);
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => _inner.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);
        public override char GetChar(int ordinal) => _inner.GetChar(ordinal);
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => _inner.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);
        public override string GetDataTypeName(int ordinal) => _inner.GetDataTypeName(ordinal);
        public override DateTime GetDateTime(int ordinal) => _inner.GetDateTime(ordinal);
        public override decimal GetDecimal(int ordinal) => _inner.GetDecimal(ordinal);
        public override double GetDouble(int ordinal) => _inner.GetDouble(ordinal);
        public override System.Collections.IEnumerator GetEnumerator() => ((System.Collections.IEnumerable)_inner).GetEnumerator();
        public override Type GetFieldType(int ordinal) => _inner.GetFieldType(ordinal);
        public override float GetFloat(int ordinal) => _inner.GetFloat(ordinal);
        public override Guid GetGuid(int ordinal) => _inner.GetGuid(ordinal);
        public override short GetInt16(int ordinal) => _inner.GetInt16(ordinal);
        public override int GetInt32(int ordinal) => _inner.GetInt32(ordinal);
        public override long GetInt64(int ordinal) => _inner.GetInt64(ordinal);
        public override string GetName(int ordinal) => _inner.GetName(ordinal);
        public override int GetOrdinal(string name) => _inner.GetOrdinal(name);
        public override string GetString(int ordinal) => _inner.GetString(ordinal);
        public override object GetValue(int ordinal) => _inner.GetValue(ordinal);
        public override int GetValues(object[] values) => _inner.GetValues(values);
        public override bool IsDBNull(int ordinal) => _inner.IsDBNull(ordinal);
        public override bool NextResult() => _inner.NextResult();
        public override bool Read() => _inner.Read();
    }

    private class InMemoryParameterCollection : DbParameterCollection
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

    private class InMemoryParameter : DbParameter
    {
        public override System.Data.DbType DbType { get; set; }
        public override System.Data.ParameterDirection Direction { get; set; } = System.Data.ParameterDirection.Input;
        public override bool IsNullable { get; set; }
#pragma warning disable CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).
        public override string ParameterName { get; set; } = string.Empty;
#pragma warning restore CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).
        public override int Size { get; set; }
#pragma warning disable CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).
        public override string SourceColumn { get; set; } = string.Empty;
#pragma warning restore CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).
        public override bool SourceColumnNullMapping { get; set; }
        public override object? Value { get; set; }
        public override void ResetDbType() { }
    }
}
