// ============================================================================
// File: IConnectionStringResolver.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-04
// Description: Abstraction for resolving connection names to connection strings.
// ============================================================================

namespace ReportSyncer.Console.Hosting;

/// <summary>
/// Provides resolution of connection names to connection strings from configuration.
/// </summary>
/// <remarks>
/// This interface enables the Console Host to map connection names referenced in sync jobs
/// to their actual connection strings defined in the YAML configuration file.
/// </remarks>
public interface IConnectionStringResolver
{
    /// <summary>
    /// Retrieves the connection string for the specified connection name.
    /// </summary>
    /// <param name="connectionName">The name of the connection to resolve.</param>
    /// <returns>The connection string associated with the connection name.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection name is not found in the configuration.
    /// </exception>
    string Get(string connectionName);
}
