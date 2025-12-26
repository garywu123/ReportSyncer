// Author: Gary Wu
// Project: ReportSyncer
// Date: 2025-12-25

using System.Threading;
using System.Threading.Tasks;
using ReportSyncer.Core.Configuration;
using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Core.Sync
{
    /// <summary>
    /// Orchestrates execution of a sync job end-to-end: configuration -> preflight -> execution.
    /// </summary>
    public interface ISyncOrchestrator
    {
        /// <summary>
        /// Runs a single sync job identified by <paramref name="jobName"/> from the configuration
        /// at <paramref name="configPath"/>. The method performs loading and validation of
        /// configuration, runs pre-flight checks, and dispatches the execution (or returns a
        /// dry-run result when applicable).
        /// </summary>
        /// <param name="configPath">Path to the configuration file or resource.</param>
        /// <param name="jobName">Name of the job to run (case-insensitive).</param>
        /// <param name="overrides">Optional runtime overrides to apply when loading configuration.</param>
        /// <param name="ct">A <see cref="CancellationToken"/> that may be used to cancel the operation.</param>
        /// <returns>A <see cref="JobResult"/> summarizing the job outcome.</returns>
        Task<JobResult> RunJobAsync(
            string configPath,
            string jobName,
            RuntimeOverrides? overrides,
            CancellationToken ct);
    }
}
