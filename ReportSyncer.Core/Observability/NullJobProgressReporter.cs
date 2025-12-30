// ============================================================================
// File: NullJobProgressReporter.cs
// Author: Codex
// Project: ReportSyncer
// Description: No-op progress reporter implementation.
// ============================================================================

namespace ReportSyncer.Core.Observability;

public sealed class NullJobProgressReporter : IJobProgressReporter
{
    public static readonly NullJobProgressReporter Instance = new();
    private NullJobProgressReporter() { }

    public void Report(JobProgressEvent evt) { }

    public void Report(TableProgressEvent evt) { }
}
