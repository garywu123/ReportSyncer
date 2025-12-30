// ============================================================================
// File: IJobProgressReporter.cs
// Author: Codex
// Project: ReportSyncer
// Description: Observer interface for job and table progress events.
// ============================================================================

namespace ReportSyncer.Core.Observability;

public interface IJobProgressReporter
{
    void Report(JobProgressEvent evt);

    void Report(TableProgressEvent evt);
}
