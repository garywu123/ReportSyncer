// Author: Gary Wu
// Project: ReportSyncer
// Date: 2025-12-25

using System;
using System.Collections.Generic;

namespace ReportSyncer.Core.Sync.Contracts
{
    /// <summary>
    /// Result summary for a sync job.
    /// </summary>
    public sealed record JobResult(
        string JobName,
        JobStatus Status,
        bool DryRun,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset? FinishedAtUtc,
        IReadOnlyList<TableResult> Tables,
        string? ErrorCode = null,
        string? ErrorMessage = null
    );
}
