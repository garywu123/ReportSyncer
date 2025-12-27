// ============================================================================
// File: InsertCommandContext.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: Context for building select and insert commands for sync operations.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using ReportSyncer.Core.Schema.Mapping;

namespace ReportSyncer.Core.Sync.Contracts;

/// <summary>
/// Represents the context required to select from a source table and insert into a target table.
/// </summary>
public sealed record InsertCommandContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InsertCommandContext"/> record.
    /// </summary>
    /// <param name="sourceSchema">Source schema name.</param>
    /// <param name="sourceTable">Source table name.</param>
    /// <param name="targetSchema">Target schema name.</param>
    /// <param name="targetTable">Target table name.</param>
    /// <param name="filters">Filters to apply when selecting or deleting rows.</param>
    /// <param name="contextColumnName">Optional context column name used to narrow source rows.</param>
    /// <param name="contextValue">Optional context value used with <paramref name="contextColumnName"/>.</param>
    /// <param name="tableMapping">Mapping information for source→target column mapping.</param>
    /// <param name="enableIdentityInsert">Whether identity insert should be enabled on the target.</param>
    /// <param name="batchSize">Batch size for batched insert operations.</param>
    public InsertCommandContext(
        string sourceSchema,
        string sourceTable,
        string targetSchema,
        string targetTable,
        IReadOnlyList<FilterPredicate> filters,
        string? contextColumnName,
        object? contextValue,
        TableMapping tableMapping,
        bool enableIdentityInsert,
        int batchSize)
    {
        ArgumentNullException.ThrowIfNull(sourceSchema);
        ArgumentNullException.ThrowIfNull(sourceTable);
        ArgumentNullException.ThrowIfNull(targetSchema);
        ArgumentNullException.ThrowIfNull(targetTable);
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentNullException.ThrowIfNull(tableMapping);

        if (string.IsNullOrWhiteSpace(sourceSchema))
            throw new ArgumentException("Source schema cannot be empty or whitespace.", nameof(sourceSchema));
        if (string.IsNullOrWhiteSpace(sourceTable))
            throw new ArgumentException("Source table cannot be empty or whitespace.", nameof(sourceTable));
        if (string.IsNullOrWhiteSpace(targetSchema))
            throw new ArgumentException("Target schema cannot be empty or whitespace.", nameof(targetSchema));
        if (string.IsNullOrWhiteSpace(targetTable))
            throw new ArgumentException("Target table cannot be empty or whitespace.", nameof(targetTable));
        if (contextColumnName is not null && string.IsNullOrWhiteSpace(contextColumnName))
            throw new ArgumentException("Context column name cannot be empty or whitespace.", nameof(contextColumnName));
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "Batch size must be greater than zero.");

        var filterCopy = filters.ToArray();
        if (filterCopy.Any(f => f is null))
            throw new ArgumentException("Filters cannot contain null entries.", nameof(filters));
        Filters = Array.AsReadOnly(filterCopy);

        SourceSchema = sourceSchema;
        SourceTable = sourceTable;
        TargetSchema = targetSchema;
        TargetTable = targetTable;
        ContextColumnName = contextColumnName;
        ContextValue = contextValue;
        TableMapping = tableMapping;
        EnableIdentityInsert = enableIdentityInsert;
        BatchSize = batchSize;
    }

    public string SourceSchema { get; }

    public string SourceTable { get; }

    public string TargetSchema { get; }

    public string TargetTable { get; }

    /// <summary>Filters applied to the source or target when building commands.</summary>
    public IReadOnlyList<FilterPredicate> Filters { get; }

    public string? ContextColumnName { get; }

    public object? ContextValue { get; }

    public TableMapping TableMapping { get; }

    public bool EnableIdentityInsert { get; }

    public int BatchSize { get; }
}
