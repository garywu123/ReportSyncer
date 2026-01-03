// Author: Gary Wu
// Project: ReportSyncer
// Date: 2025-12-25

using System;
using System.Collections.Generic;

namespace ReportSyncer.Core.Sync.Contracts;

/// <summary>
/// Result summary for a sync job.
/// </summary>
public sealed record JobResult
{
    public string                     JobName       { get; init; }
    public JobStatus                  Status        { get; init; }
    public bool                       DryRun        { get; init; }
    public DateTimeOffset             StartedAtUtc  { get; init; }
    public DateTimeOffset?            FinishedAtUtc { get; init; }
    public IReadOnlyList<TableResult> Tables        { get; init; }
    public string?                    ErrorCode     { get; init; }
    public string?                    ErrorMessage  { get; init; }

    public JobResult(
        string                     jobName,
        JobStatus                  status,
        bool                       dryRun,
        DateTimeOffset             startedAtUtc,
        DateTimeOffset?            finishedAtUtc,
        IReadOnlyList<TableResult> tables,
        string?                    errorCode    = null,
        string?                    errorMessage = null)
    {
        JobName = jobName ?? throw new ArgumentNullException(nameof(jobName));
        Status = status;
        DryRun = dryRun;
        StartedAtUtc = startedAtUtc;
        FinishedAtUtc = finishedAtUtc;
        Tables = (tables as TableResult[]) ?? (tables is null ? Array.Empty<TableResult>() : new List<TableResult>(tables).ToArray());
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }
}