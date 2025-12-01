// ============================================================================
// File: SyncConfiguration.cs
// Author: Gary Wu
// Date: 2025-12-01
// Project: ReportSyncer
// Description: Root configuration model representing the entire YAML configuration file.
// ============================================================================

namespace ReportSyncer.Core.Configuration
{
    /// <summary>
    /// Represents the complete synchronization configuration loaded from a YAML file,
    /// including run settings, safety rules, schema policies, database connections, and sync jobs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the root configuration object that serves as the entry point for all
    /// synchronization operations. It aggregates:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <b>Version:</b> Configuration file format version for future compatibility.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>Run settings:</b> Global runtime options like dry-run mode and batch sizes.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>Safety rules:</b> Protection against Prod→Prod syncs and large deletes.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>Schema policy:</b> How to handle schema mismatches and validation.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>Connections:</b> Named database connections with environment and type metadata.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>Sync jobs:</b> Logical units of work that define what data to sync and how.
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// After loading from YAML, this configuration is validated to ensure reference integrity
    /// (e.g., jobs reference valid connections) and semantic correctness (e.g., safety rules
    /// are satisfied).
    /// </para>
    /// </remarks>
    /// <example>
    /// <![CDATA[
    /// // Complete configuration example
    /// var config = new SyncConfiguration(
    ///     version: "1.2",
    ///     run: new RunConfig(
    ///         dryRun: false,
    ///         defaultBatchSize: 2000,
    ///         deleteChunkSize: 5000,
    ///         useTvpIfAvailable: true
    ///     ),
    ///     safety: new SafetyConfig(
    ///         forbidProdToProd: true,
    ///         requireDifferentConnections: true,
    ///         confirmLargeDeletePct: 0.8
    ///     ),
    ///     schemaPolicy: new SchemaPolicyConfig(
    ///         onMismatch: SchemaMismatchBehavior.Fail,
    ///         requirePrimaryKey: true,
    ///         allowExtraTargetColumns: true
    ///     ),
    ///     connections: new[] {
    ///         new ConnectionConfig(
    ///             name: "AppDB_Prod",
    ///             connectionString: "Server=prod;Database=App;Trusted_Connection=true;",
    ///             environment: EnvironmentType.Prod,
    ///             type: ConnectionType.Application
    ///         ),
    ///         new ConnectionConfig(
    ///             name: "ReportDB_Dev",
    ///             connectionString: "Server=dev;Database=Report;Trusted_Connection=true;",
    ///             environment: EnvironmentType.Dev,
    ///             type: ConnectionType.Reporting
    ///         )
    ///     },
    ///     syncJobs: new[] {
    ///         new SyncJobConfig(
    ///             name: "Sync-Customers",
    ///             description: "Sync customer dimension from Prod to Dev",
    ///             sourceConnection: "AppDB_Prod",
    ///             targetConnection: "ReportDB_Dev",
    ///             parameters: new Dictionary<string, string> {
    ///                 { "CustomerId", "101" }
    ///             },
    ///             tables: new[] {
    ///                 new TableTaskConfig(
    ///                     source: "Customers",
    ///                     target: "Customers",
    ///                     preSyncTargetAction: true,
    ///                     allowAllDelete: true
    ///                 )
    ///             }
    ///         )
    ///     }
    /// );
    /// ]]>
    /// </example>
    public class SyncConfiguration
    {
        private readonly ConnectionConfig[] _connections;
        private readonly SyncJobConfig[] _syncJobs;

        /// <summary>
        /// Gets the configuration file format version.
        /// </summary>
        /// <remarks>
        /// Used for future compatibility when configuration schema evolves.
        /// Current version: "1.2".
        /// </remarks>
        public string Version { get; }

        /// <summary>
        /// Gets the global run settings for synchronization execution.
        /// </summary>
        /// <remarks>
        /// Controls dry-run mode, batch sizes, delete chunk sizes, and TVP usage.
        /// These settings apply globally unless overridden at the table level.
        /// </remarks>
        public RunConfig Run { get; }

        /// <summary>
        /// Gets the global safety rules to prevent dangerous operations.
        /// </summary>
        /// <remarks>
        /// Enforces protections such as forbidding Prod→Prod syncs, preventing self-syncs,
        /// and requiring confirmation for large deletes.
        /// </remarks>
        public SafetyConfig Safety { get; }

        /// <summary>
        /// Gets the schema validation policies.
        /// </summary>
        /// <remarks>
        /// Controls how the system responds to schema mismatches, primary key requirements,
        /// and extra target columns during pre-flight validation.
        /// </remarks>
        public SchemaPolicyConfig SchemaPolicy { get; }

        /// <summary>
        /// Gets the list of named database connections.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Connections are referenced by name from sync jobs. Each connection includes:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Connection string for SQL Server connectivity</description></item>
        /// <item><description>Environment classification (Prod, Dev, Test, etc.)</description></item>
        /// <item><description>Type classification (Application, Reporting)</description></item>
        /// </list>
        /// <para>
        /// At least one connection must be defined in a valid configuration.
        /// </para>
        /// </remarks>
        public IReadOnlyList<ConnectionConfig> Connections => _connections;

        /// <summary>
        /// Gets the list of sync jobs to execute.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Each sync job defines a logical unit of work that:
        /// </para>
        /// <list type="bullet">
        /// <item><description>References source and target connections</description></item>
        /// <item><description>Defines parameters for filter resolution and context injection</description></item>
        /// <item><description>Contains one or more table tasks</description></item>
        /// </list>
        /// <para>
        /// At least one sync job must be defined in a valid configuration.
        /// </para>
        /// </remarks>
        public IReadOnlyList<SyncJobConfig> SyncJobs => _syncJobs;

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncConfiguration"/> class.
        /// </summary>
        /// <remarks>
        /// <param name="version">
        /// The configuration file format version. Must not be null or empty.
        /// </param>
        /// <param name="run">
        /// The global run settings. Must not be null.
        /// </param>
        /// <param name="safety">
        /// The global safety rules. Must not be null.
        /// </param>
        /// <param name="schemaPolicy">
        /// The schema validation policies. Must not be null.
        /// </param>
        /// <param name="connections">
        /// The list of database connections. Must contain at least one connection.
        /// </param>
        /// <param name="syncJobs">
        /// The list of sync jobs. Must contain at least one job.
        /// </param>
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Thrown when any required parameter is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="version"/> is empty or whitespace, or when
        /// <paramref name="connections"/> or <paramref name="syncJobs"/> is empty.
        /// </exception>
        public SyncConfiguration(
            string version,
            RunConfig run,
            SafetyConfig safety,
            SchemaPolicyConfig schemaPolicy,
            IEnumerable<ConnectionConfig> connections,
            IEnumerable<SyncJobConfig> syncJobs)
        {
            if (version == null)
                throw new ArgumentNullException(nameof(version));
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Configuration version cannot be empty or whitespace.", nameof(version));
            if (run == null)
                throw new ArgumentNullException(nameof(run));
            if (safety == null)
                throw new ArgumentNullException(nameof(safety));
            if (schemaPolicy == null)
                throw new ArgumentNullException(nameof(schemaPolicy));
            if (connections == null)
                throw new ArgumentNullException(nameof(connections));
            if (syncJobs == null)
                throw new ArgumentNullException(nameof(syncJobs));

            _connections = connections.ToArray();
            if (_connections.Length == 0)
                throw new ArgumentException("Configuration must contain at least one connection.", nameof(connections));

            _syncJobs = syncJobs.ToArray();
            if (_syncJobs.Length == 0)
                throw new ArgumentException("Configuration must contain at least one sync job.", nameof(syncJobs));

            Version = version;
            Run = run;
            Safety = safety;
            SchemaPolicy = schemaPolicy;
        }
    }
}
