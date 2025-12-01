// ============================================================================
// File: IConfigurationLoader.cs
// Author: Gary Wu
// Date: 2025-12-01
// Project: ReportSyncer
// Description: Abstraction for loading sync configuration from a source.
// ============================================================================

using System.Threading;
using System.Threading.Tasks;

namespace ReportSyncer.Core.Configuration
{
    /// <summary>
    /// Loads a <see cref="SyncConfiguration"/> from a source (file/stream).
    /// Implementations must wrap parsing/structural errors in <see cref="ConfigurationException"/>.
    /// </summary>
    public interface IConfigurationLoader
    {
        /// <summary>
        /// Load a configuration from the specified path.
        /// </summary>
        /// <param name="path">Path to the YAML configuration file.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The parsed <see cref="SyncConfiguration"/>.</returns>
        Task<SyncConfiguration> LoadAsync(string path, CancellationToken ct);
    }
}
