using System.Data;
using DotNetToolkit.Database.Abstractions;

namespace ReportSyncer.Core.Tests.Helpers.TestDoubles.Security;

/// <summary>
/// Minimal IDbContext test double that records executed commands and can throw via a delegate.
/// </summary>
internal sealed class RecordingDbContext : IDbContext
{
    public List<RecordingCommand> CreatedCommands { get; } = new();

    public List<RecordingCommand> ExecutedCommands { get; } = new();

    public Func<RecordingCommand, Task<int>> OnExecuteNonQueryAsync { get; set; } = _ => Task.FromResult(0);

    public IDbCommandWrapper CreateCommand(string storedProcedureName)
    {
        return CreateCommand(storedProcedureName, CommandType.StoredProcedure);
    }

    public IDbCommandWrapper CreateCommand(string commandText, CommandType commandType)
    {
        var cmd = new RecordingCommand(commandText, commandType);
        CreatedCommands.Add(cmd);
        return cmd;
    }

    public async Task<int> ExecuteNonQueryAsync(IDbCommandWrapper command, CancellationToken cancellationToken = default)
    {
        var recording = (RecordingCommand)command;
        ExecutedCommands.Add(recording);
        cancellationToken.ThrowIfCancellationRequested();
        return await OnExecuteNonQueryAsync(recording).ConfigureAwait(false);
    }

    public Task<List<T>> ExecuteQueryAsync<T>(IDbCommandWrapper command, CancellationToken cancellationToken = default) where T : new()
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new List<T>());
    }

    public void Dispose() { }
}

internal sealed class RecordingCommand : IDbCommandWrapper
{
    public RecordingCommand(string commandText, CommandType commandType)
    {
        CommandText = commandText;
        CommandType = commandType;
    }

    public string CommandText { get; }

    public CommandType CommandType { get; }

    public IEnumerable<IDbDataParameter> Parameters => Array.Empty<IDbDataParameter>();

    public void AddParameter(string name, object value, DbType type, ParameterDirection direction = ParameterDirection.Input)
    {
        // Parameters are not required for permission probes in current tests.
    }

    public T GetParameterValue<T>(string name) => default!;
}
