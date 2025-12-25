// Author: Gary Wu
// Project: ReportSyncer
// Date: 2025-12-25

namespace ReportSyncer.Core.Sync.Contracts
{
    /// <summary>
    /// Status of a single table within a sync job.
    /// </summary>
    public enum TableStatus
    {
        Planned = 0,
        SkippedDryRun = 1,
        Succeeded = 2,
        FailedPreFlight = 3,
        FailedExecution = 4,
        Cancelled = 5
    }
}
