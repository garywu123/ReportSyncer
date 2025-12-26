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
    /// Performs pre-flight validation for a sync job.
    /// </summary>
    public interface IPreFlightValidator
    {
        /// <summary>
        /// Validates the specified <paramref name="job"/> using the provided
        /// <paramref name="effectiveConfig"/>. Includes schema checks and other
        /// safety validations performed prior to executing a sync.
        /// </summary>
        /// <param name="effectiveConfig">The effective runtime configuration for the sync run.</param>
        /// <param name="job">The sync job configuration to validate.</param>
        /// <param name="ct">A <see cref="CancellationToken"/> used to cancel the operation.</param>
        /// <returns>A <see cref="PreFlightResult"/> describing the outcome of the pre-flight checks.</returns>
        Task<PreFlightResult> ValidateAsync(
            SyncConfiguration effectiveConfig,
            SyncJobConfig job,
            CancellationToken ct);
    }
}
