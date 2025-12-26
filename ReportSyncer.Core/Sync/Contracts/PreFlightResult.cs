// Author: Gary Wu
// Project: ReportSyncer
// Date: 2025-12-25

using System;
using System.Collections.Generic;
using ReportSyncer.Core.Schema.Dependency;

namespace ReportSyncer.Core.Sync.Contracts;

/// <summary>
/// Result of pre-flight validation for a job.
/// </summary>
public sealed record PreFlightResult
{
    public string JobName { get; init; }
    public bool DryRun { get; init; }
    public ExecutionPlan ExecutionPlan { get; init; }
    public IReadOnlyList<TableResult> PlannedTables { get; init; }
    public IReadOnlyList<string> Warnings { get; init; }

    public PreFlightResult(
        string jobName,
        bool dryRun,
        ExecutionPlan executionPlan,
        IReadOnlyList<TableResult> plannedTables,
        IReadOnlyList<string> warnings)
    {
        JobName = jobName ?? throw new ArgumentNullException(nameof(jobName));
        DryRun = dryRun;
        ExecutionPlan = executionPlan ?? throw new ArgumentNullException(nameof(executionPlan));
        PlannedTables = (plannedTables as TableResult[]) ?? (plannedTables is null ? Array.Empty<TableResult>() : new List<TableResult>(plannedTables).ToArray());
        Warnings = (warnings as string[]) ?? (warnings is null ? Array.Empty<string>() : new List<string>(warnings).ToArray());
    }
}

