// ============================================================================
// File: SyncConfiguration.cs
// Author: Gary Wu
// Date: 2025-12-01
// Project: ReportSyncer
// Description: Root configuration model representing the entire YAML configuration file.
// ============================================================================

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace ReportSyncer.Core.Configuration;

/// <summary>
/// Root configuration for synchronization jobs (represents the YAML config file).
/// </summary>
public class SyncConfiguration
{
    private readonly ConnectionConfig[] _connections;
    private readonly SyncJobConfig[]    _syncJobs;

    /// <summary>
    /// Gets the configuration file format version (e.g. "1.2").
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Global run settings (dry-run, batch sizes, TVP usage).
    /// </summary>
    public RunConfig Run { get; }

    /// <summary>
    /// Global safety rules to prevent dangerous operations.
    /// </summary>
    public SafetyConfig Safety { get; }

    /// <summary>
    /// Schema validation policies used during pre-flight checks.
    /// </summary>
    public SchemaPolicyConfig SchemaPolicy { get; }

    /// <summary>
    /// Named database connections referenced by sync jobs.
    /// </summary>
    public IReadOnlyList<ConnectionConfig> Connections => _connections;

    /// <summary>
    /// Sync jobs to execute.
    /// </summary>
    public IReadOnlyList<SyncJobConfig> SyncJobs => _syncJobs;

    /// <summary>
    /// Create a new sync configuration.
    /// </summary>
    /// <param name="version">Configuration format version (non-empty).</param>
    /// <param name="run">Global run settings (required).</param>
    /// <param name="safety">Global safety rules (required).</param>
    /// <param name="schemaPolicy">Schema validation policies (required).</param>
    /// <param name="connections">Named database connections (at least one required).</param>
    /// <param name="syncJobs">Sync jobs to execute (at least one required).</param>
    public SyncConfiguration(string                        version,
                             RunConfig                     run,
                             SafetyConfig                  safety,
                             SchemaPolicyConfig            schemaPolicy,
                             IEnumerable<ConnectionConfig> connections,
                             IEnumerable<SyncJobConfig>    syncJobs)
    {
        ArgumentNullException.ThrowIfNull(version);
        Version = version ?? throw new ArgumentNullException(nameof(version));

        Run = run                   ?? throw new ArgumentNullException(nameof(run));
        Safety = safety             ?? throw new ArgumentNullException(nameof(safety));
        SchemaPolicy = schemaPolicy ?? throw new ArgumentNullException(nameof(schemaPolicy));
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException(
                "Configuration version cannot be empty or whitespace.", nameof(version)
            );

        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(syncJobs);

        _connections = connections.ToArray();
        if (_connections.Length == 0)
            throw new ArgumentException(
                "Configuration must contain at least one connection.", nameof(connections)
            );

        _syncJobs = syncJobs.ToArray();
        if (_syncJobs.Length == 0)
            throw new ArgumentException(
                "Configuration must contain at least one sync job.", nameof(syncJobs)
            );
    }
}
