// ============================================================================
// File: IConfigurationValidator.cs
// Author: Gary Wu
// Date: 2025-12-02
// Project: ReportSyncer
// Description: Interface for configuration validation that enforces all static rules before runtime execution.
// ============================================================================

namespace ReportSyncer.Core.Configuration;

/// <summary>
/// Validates a <see cref="SyncConfiguration"/> against all static rules.
/// </summary>
/// <remarks>
/// Collects all violations in a single pass and throws a single <see cref="ConfigurationException"/>
/// containing all errors, rather than failing on the first error.
/// </remarks>
public interface IConfigurationValidator
{
    /// <summary>
    /// Validates the given configuration.
    /// </summary>
    /// <param name="configuration">The configuration to validate. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if configuration is null.</exception>
    /// <exception cref="ConfigurationException">
    /// Thrown when validation fails. Contains all errors found, with each error including
    /// a code (e.g., "CFG_CONNECTIONS_EMPTY"), message, and path within the config tree.
    /// </exception>
    void Validate(SyncConfiguration configuration);
}
