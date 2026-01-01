// ============================================================================
// File: TableExecutionContext.cs
// Author: Gary Wu
// Project: ReportSyncer
// Description: Aggregates runtime context required to execute table sync operations.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Schema.Dependency;
using ReportSyncer.Core.Schema.Mapping;

namespace ReportSyncer.Core.Sync.Contracts;

/// <summary>
/// Represents the immutable context for executing sync operations against a single table pair.
/// </summary>
public sealed record TableExecutionContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TableExecutionContext"/> record.
    /// </summary>
    /// <param name="jobId">Unique identifier for the sync job.</param>
    /// <param name="jobName">Human-readable job name.</param>
    /// <param name="sourceConnectionName">Named source connection.</param>
    /// <param name="targetConnectionName">Named target connection.</param>
    /// <param name="sourceDbType">Source database engine type.</param>
    /// <param name="targetDbType">Target database engine type.</param>
    /// <param name="sourceSchema">Source schema name.</param>
    /// <param name="sourceTable">Source table name.</param>
    /// <param name="targetSchema">Target schema name.</param>
    /// <param name="targetTable">Target table name.</param>
    /// <param name="dryRun">If true, no changes are written to the target.</param>
    /// <param name="preSyncTargetDelete">If true, perform a pre-sync delete on the target.</param>
    /// <param name="enableIdentityInsert">Whether identity insert should be enabled on the target.</param>
    /// <param name="contextColumnName">Optional context column name used to narrow source rows.</param>
    /// <param name="contextValue">Optional context value used with <paramref name="contextColumnName"/>.</param>
    /// <param name="filters">Filters applied for the execution.</param>
    /// <param name="tableMapping">Table mapping information for the sync.</param>
    /// <param name="executionPlan">Execution plan for the sync.</param>
    /// <param name="batchSize">Batch size for batched operations.</param>
    /// <param name="etaSmoothing">Optional ETA smoothing factor (0..1) for progress tracking.</param>
    public TableExecutionContext(
        Guid jobId,
        string jobName,
        string sourceConnectionName,
        string targetConnectionName,
        DatabaseType sourceDbType,
        DatabaseType targetDbType,
        string sourceSchema,
        string sourceTable,
        string targetSchema,
        string targetTable,
        bool dryRun,
        bool preSyncTargetDelete,
        bool enableIdentityInsert,
        string? contextColumnName,
        object? contextValue,
        IReadOnlyList<FilterPredicate> filters,
        TableMapping tableMapping,
        ExecutionPlan executionPlan,
        int batchSize,
        double? etaSmoothing)
    {
        ArgumentNullException.ThrowIfNull(jobName);
        ArgumentNullException.ThrowIfNull(sourceConnectionName);
        ArgumentNullException.ThrowIfNull(targetConnectionName);
        ArgumentNullException.ThrowIfNull(sourceSchema);
        ArgumentNullException.ThrowIfNull(sourceTable);
        ArgumentNullException.ThrowIfNull(targetSchema);
        ArgumentNullException.ThrowIfNull(targetTable);
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentNullException.ThrowIfNull(tableMapping);
        ArgumentNullException.ThrowIfNull(executionPlan);

        if (string.IsNullOrWhiteSpace(jobName))
            throw new ArgumentException("Job name cannot be empty or whitespace.", nameof(jobName));
        if (string.IsNullOrWhiteSpace(sourceConnectionName))
            throw new ArgumentException("Source connection name cannot be empty or whitespace.", nameof(sourceConnectionName));
        if (string.IsNullOrWhiteSpace(targetConnectionName))
            throw new ArgumentException("Target connection name cannot be empty or whitespace.", nameof(targetConnectionName));
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
        if (etaSmoothing.HasValue && (etaSmoothing.Value < 0.0 || etaSmoothing.Value > 1.0))
            throw new ArgumentOutOfRangeException(nameof(etaSmoothing), etaSmoothing, "ETA smoothing must be between 0 and 1.");

        var filterCopy = filters.ToArray();
        if (filterCopy.Any(f => f is null))
            throw new ArgumentException("Filters cannot contain null entries.", nameof(filters));
        Filters = Array.AsReadOnly(filterCopy);

        JobId = jobId;
        JobName = jobName;
        SourceConnectionName = sourceConnectionName;
        TargetConnectionName = targetConnectionName;
        SourceDbType = sourceDbType;
        TargetDbType = targetDbType;
        SourceSchema = sourceSchema;
        SourceTable = sourceTable;
        TargetSchema = targetSchema;
        TargetTable = targetTable;
        DryRun = dryRun;
        PreSyncTargetDelete = preSyncTargetDelete;
        EnableIdentityInsert = enableIdentityInsert;
        ContextColumnName = contextColumnName;
        ContextValue = contextValue;
        TableMapping = tableMapping;
        ExecutionPlan = executionPlan;
        BatchSize = batchSize;
        EtaSmoothing = etaSmoothing;
    }

    public Guid JobId { get; }

    public string JobName { get; }

    public string SourceConnectionName { get; }

    public string TargetConnectionName { get; }

    public DatabaseType SourceDbType { get; }

    public DatabaseType TargetDbType { get; }

    public string SourceSchema { get; }

    public string SourceTable { get; }

    public string TargetSchema { get; }

    public string TargetTable { get; }

    public bool DryRun { get; }

    public bool PreSyncTargetDelete { get; }

    public bool EnableIdentityInsert { get; }

    public string? ContextColumnName { get; }

    public object? ContextValue { get; }

    /// <summary>Filters applied during execution.</summary>
    public IReadOnlyList<FilterPredicate> Filters { get; }

    /// <summary>Mapping information for columns between source and target.</summary>
    public TableMapping TableMapping { get; }

    /// <summary>Execution plan describing steps for syncing.</summary>
    public ExecutionPlan ExecutionPlan { get; }

    /// <summary>Batch size to use for batched operations.</summary>
    public int BatchSize { get; }

    /// <summary>Optional ETA smoothing factor (0..1) for progress tracking throughput calculation.</summary>
    public double? EtaSmoothing { get; }
}
