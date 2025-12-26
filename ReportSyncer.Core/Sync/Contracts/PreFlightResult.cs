// Author: Gary Wu
// Project: ReportSyncer
// Date: 2025-12-25

using System;
using ReportSyncer.Core.Schema.Services;

namespace ReportSyncer.Core.Sync.Contracts;

/// <summary>
/// Result of pre-flight validation for a job.
/// </summary>
public sealed record PreFlightResult
{
    public string JobName { get; init; }
    public bool DryRun { get; init; }
    public SchemaAnalysisResult Schema { get; init; }

    public PreFlightResult(
        string jobName,
        bool dryRun,
        SchemaAnalysisResult schema)
    {
        JobName = jobName ?? throw new ArgumentNullException(nameof(jobName));
        DryRun = dryRun;
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));
    }
}
