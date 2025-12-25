// Author: Gary Wu
// Project: ReportSyncer
// Date: 2025-12-25

namespace ReportSyncer.Core.Sync.Contracts
{
    /// <summary>
    /// Overall status of a sync job.
    /// </summary>
    public enum JobStatus
    {
        Succeeded = 0,
        FailedPreFlight = 1,
        FailedExecution = 2,
        Cancelled = 3,
        SkippedDryRun = 4
    }
}
