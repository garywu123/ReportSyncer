// ============================================================================
// File: DeleteCommandContext.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: Context for building delete commands against a target table.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace ReportSyncer.Core.Sync.Contracts;

/// <summary>
/// Represents the context needed to build a delete command for a target table.
/// </summary>
public sealed record DeleteCommandContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteCommandContext"/> record.
    /// </summary>
    /// <param name="targetSchema">Target schema name.</param>
    /// <param name="targetTable">Target table name.</param>
    /// <param name="filters">Filters to apply when deleting rows on the target.</param>
    public DeleteCommandContext(string targetSchema, string targetTable, IReadOnlyList<FilterPredicate> filters)
    {
        ArgumentNullException.ThrowIfNull(targetSchema);
        ArgumentNullException.ThrowIfNull(targetTable);
        ArgumentNullException.ThrowIfNull(filters);

        if (string.IsNullOrWhiteSpace(targetSchema))
            throw new ArgumentException("Target schema cannot be empty or whitespace.", nameof(targetSchema));
        if (string.IsNullOrWhiteSpace(targetTable))
            throw new ArgumentException("Target table cannot be empty or whitespace.", nameof(targetTable));

        var filterCopy = filters.ToArray();
        if (filterCopy.Any(f => f is null))
            throw new ArgumentException("Filters cannot contain null entries.", nameof(filters));
        Filters = Array.AsReadOnly(filterCopy);

        TargetSchema = targetSchema;
        TargetTable = targetTable;
    }

    /// <summary>Target schema name for the delete operation.</summary>
    public string TargetSchema { get; }

    /// <summary>Target table name for the delete operation.</summary>
    public string TargetTable { get; }

    /// <summary>Filters applied to identify rows to delete.</summary>
    public IReadOnlyList<FilterPredicate> Filters { get; }
}
