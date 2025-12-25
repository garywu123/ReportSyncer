// Author: Gary Wu
// Project: ReportSyncer
// Date: 2025-12-25

using System.Collections.Generic;
using ReportSyncer.Core.Schema.Dependency;

namespace ReportSyncer.Core.Sync.Contracts
{
    /// <summary>
    /// Result of pre-flight validation for a job.
    /// </summary>
    public sealed record PreFlightResult(
        string JobName,
        bool DryRun,
        ExecutionPlan ExecutionPlan,
        IReadOnlyList<TableResult> PlannedTables,
        IReadOnlyList<string> Warnings
    );
}
