// ============================================================================
// File: DbCommandSpec.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: SQL command specification contract for execution.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ReportSyncer.Core.Sync.Contracts;

/// <summary>
/// Represents a fully constructed SQL command and its parameters.
/// </summary>
public sealed record DbCommandSpec
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DbCommandSpec"/> record.
    /// </summary>
    /// <param name="sql">SQL text to execute.</param>
    /// <param name="parameters">Ordered list of parameters for the SQL.</param>
    public DbCommandSpec(string sql, IReadOnlyList<CommandParameterSpec> parameters)
    {
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameters);

        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("SQL text cannot be empty or whitespace.", nameof(sql));

        var parameterCopy = parameters.ToArray();
        if (parameterCopy.Any(p => p is null))
            throw new ArgumentException("Parameters cannot contain null entries.", nameof(parameters));
        Parameters = Array.AsReadOnly(parameterCopy);

        Sql = sql;
    }

    /// <summary>SQL text to execute.</summary>
    public string Sql { get; }

    /// <summary>Ordered list of parameters for the SQL text.</summary>
    public IReadOnlyList<CommandParameterSpec> Parameters { get; }
}

/// <summary>
/// Represents a single SQL parameter.
/// </summary>
public sealed record CommandParameterSpec
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CommandParameterSpec"/> record.
    /// </summary>
    /// <param name="name">Parameter name (including leading @).</param>
    /// <param name="value">Parameter value.</param>
    /// <param name="dbType">Optional database type hint.</param>
    public CommandParameterSpec(string name, object? value, DbType? dbType = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Parameter name cannot be empty or whitespace.", nameof(name));

        Name = name;
        Value = value;
        DbType = dbType;
    }

    /// <summary>Parameter name (including leading @).</summary>
    public string Name { get; }

    /// <summary>Parameter value.</summary>
    public object? Value { get; }

    /// <summary>Optional database type hint for the parameter.</summary>
    public DbType? DbType { get; }
}
