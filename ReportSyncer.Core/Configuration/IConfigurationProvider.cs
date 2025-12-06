// ============================================================================
// File: IConfigurationProvider.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2025-12-06
// Description: Abstraction for loading and producing the effective SyncConfiguration.
// ============================================================================

using System.Threading;
using System.Threading.Tasks;

namespace ReportSyncer.Core.Configuration;

/// <summary>
/// Provides a validated, effective <see cref="SyncConfiguration"/> for execution.
/// </summary>
public interface IConfigurationProvider
{
    /// <summary>
    /// Loads and validates configuration from the given YAML file path.
    /// </summary>
    Task<SyncConfiguration> LoadAndValidateAsync(string path, CancellationToken ct);

    /// <summary>
    /// Loads the YAML configuration and applies optional runtime overrides to produce the effective configuration.
    /// </summary>
    /// <remarks>
    /// When <paramref name="overrides"/> is null or empty, this is equivalent to the parameterless overload.
    /// </remarks>
    Task<SyncConfiguration> LoadAndValidateAsync(string path, RuntimeOverrides? overrides, CancellationToken ct);
}

// Policy note:
// - Hosts must use the overload that accepts `RuntimeOverrides` when they intend to alter run-level
//   behavior for a single execution.
// - After obtaining the effective `SyncConfiguration`, all downstream components must use that
//   configuration object and must not accept or apply `RuntimeOverrides` themselves. This ensures
//   override validation and safety checks are centralized and auditable.
