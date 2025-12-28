using System.Collections;
using System.Data;
using System.Data.Common;
using DotNetToolkit.Database.Abstractions;

namespace ReportSyncer.Core.Tests.Helpers.TestDoubles.Sql;

/// <summary>
/// IDbConnectionFactory that returns connections whose ExecuteScalar dequeues predefined results.
/// Useful for COUNT-style tests.
/// </summary>
internal sealed class ScalarQueueDbConnectionFactory : IDbConnectionFactory
{
    private readonly Queue<object?> _results;

    public ScalarQueueDbConnectionFactory(params object?[] results)
    {
        _results = new Queue<object?>(results);
    }

    public IDbConnection CreateConnection() => new ScalarQueueDbConnection(_results);

    public Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IDbConnection>(new ScalarQueueDbConnection(_results));

    public DbProviderFactory GetProviderFactory() => throw new NotImplementedException();
}

internal sealed class ThrowingDbConnectionFactory : IDbConnectionFactory
{
    private readonly Exception _ex;
    public ThrowingDbConnectionFactory(Exception ex) => _ex = ex;
    public IDbConnection CreateConnection() => throw _ex;
    public Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default) => Task.FromException<IDbConnection>(_ex);
    public DbProviderFactory GetProviderFactory() => throw new NotImplementedException();
}

internal sealed class ScalarQueueDbConnection : DbConnection
{
    private readonly Queue<object?> _results;
    public List<ScalarQueueDbCommand> Commands { get; } = new();

    public ScalarQueueDbConnection(Queue<object?> results) => _results = results;

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
        var cmd = new ScalarQueueDbCommand(_results);
        Commands.Add(cmd);
        return cmd;
    }
}

internal sealed class ScalarQueueDbCommand : DbCommand
{
    private readonly Queue<object?> _results;
    public ScalarQueueDbCommand(Queue<object?> results) => _results = results;

    public override string CommandText { get; set; } = string.Empty;
    public override int CommandTimeout { get; set; }
    public override CommandType CommandType { get; set; } = CommandType.Text;
    protected override DbConnection DbConnection { get; set; } = null!;
    protected override DbParameterCollection DbParameterCollection { get; } = new ScalarQueueParameterCollection();
    protected override DbTransaction DbTransaction { get; set; } = null!;
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }

    public override void Cancel() { }
    public override int ExecuteNonQuery() => throw new NotImplementedException();
    public override object? ExecuteScalar() => _results.Count > 0 ? _results.Dequeue() : 0;
    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
    public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken) => Task.FromResult<object?>(ExecuteScalar());
    public override void Prepare() { }
    protected override DbParameter CreateDbParameter() => new ScalarQueueDbParameter();
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotImplementedException();
}

internal sealed class ScalarQueueParameterCollection : DbParameterCollection
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

internal sealed class ScalarQueueDbParameter : DbParameter
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
