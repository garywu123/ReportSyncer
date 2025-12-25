// Author: Gary Wu
// Project: ReportSyncer
// Date: 2025-12-25

using System.Threading;
using System.Threading.Tasks;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Core.Sync
{
    public interface ISyncOrchestrator
    {
        Task<JobResult> RunJobAsync(
            string configPath,
            string jobName,
            RuntimeOverrides? overrides,
            CancellationToken ct);
    }
}
