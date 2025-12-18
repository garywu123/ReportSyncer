using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using DotNetToolkit.Database.Abstractions;

#pragma warning disable CA1806
namespace ReportSyncer.Core.Tests.Helpers
{
    internal sealed class FakeCommand : IDbCommandWrapper
    {
        private readonly Dictionary<string, object?> _params = new();
        public string CommandText { get; private set; } = string.Empty;
        public CommandType CommandType { get; private set; }
        public IEnumerable<IDbDataParameter> Parameters => Array.Empty<IDbDataParameter>();

        public void AddParameter(string name, object value, DbType type, ParameterDirection direction = ParameterDirection.Input)
        {
            _params[name] = value;
        }

        public T GetParameterValue<T>(string name)
        {
            if (!_params.TryGetValue(name, out var v)) return default!;
            return (T)v!;
        }
    }

    internal sealed class FakeDbContext : IDbContext
    {
        public readonly Dictionary<string, Dictionary<string, object>[]> ColumnRows = new();
        public Dictionary<string, object>[] FkRows = Array.Empty<Dictionary<string, object>>();
        public void Dispose() { }

        public IDbCommandWrapper CreateCommand(string commandText) => new FakeCommand();
        public IDbCommandWrapper CreateCommand(string commandText, CommandType commandType)
        {
            var c = new FakeCommand();
            return c;
        }

        public Task<int> ExecuteNonQueryAsync(IDbCommandWrapper command, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<List<T>> ExecuteQueryAsync<T>(IDbCommandWrapper command, CancellationToken cancellationToken = default) where T : new()
        {
            var t = typeof(T);
            var listType = typeof(List<>).MakeGenericType(t);
            var list = (IList)Activator.CreateInstance(listType)!;

            if (t.FullName!.EndsWith("+ColumnRow"))
            {
                var table = command.GetParameterValue<string>("@table");
                var schema = command.GetParameterValue<string>("@schema");
                var key = schema + "." + table;
                if (!ColumnRows.TryGetValue(key, out var rows)) return Task.FromResult((List<T>)list);

                foreach (var dict in rows)
                {
                    var inst = Activator.CreateInstance(t, true)!;
                    foreach (var kv in dict)
                    {
                        var p = t.GetProperty(kv.Key)!;
                        p.SetValue(inst, kv.Value);
                    }
                    list.Add(inst);
                }

                return Task.FromResult((List<T>)list);
            }

            if (t.FullName!.EndsWith("+ForeignKeyRow"))
            {
                foreach (var dict in FkRows)
                {
                    var inst = Activator.CreateInstance(t, true)!;
                    foreach (var kv in dict)
                    {
                        var p = t.GetProperty(kv.Key)!;
                        p.SetValue(inst, kv.Value);
                    }
                    list.Add(inst);
                }

                return Task.FromResult((List<T>)list);
            }

            return Task.FromResult((List<T>)list);
        }
    }

    internal sealed class BadDbContext : IDbContext
    {
        public IDbCommandWrapper CreateCommand(string commandText) => throw new Exception("boom");
        public IDbCommandWrapper CreateCommand(string commandText, CommandType commandType) => throw new Exception("boom");
        public Task<int> ExecuteNonQueryAsync(IDbCommandWrapper command, CancellationToken cancellationToken = default) => throw new Exception("boom");
        public Task<List<T>> ExecuteQueryAsync<T>(IDbCommandWrapper command, CancellationToken cancellationToken = default) where T : new() => throw new Exception("boom");
        public void Dispose() { }
    }
}
