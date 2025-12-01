// ============================================================================
// File: SyncJobConfig.cs
// Author: Gary Wu
// Date: 2025-12-01
// Project: ReportSyncer
// Description: Configuration for a synchronization job containing multiple table tasks.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace ReportSyncer.Core.Configuration
{
    /// <summary>
    /// Defines a synchronization job that copies data from a source connection to a target connection,
    /// processing one or more table tasks with shared parameters.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A sync job represents a logical unit of work that:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// Connects to a source and target database using named connections.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Defines parameters (e.g., CustomerId, StartDate, EndDate) used for filtering and context injection.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Contains one or more table tasks that define what data to sync and how.
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// <b>Parameter Resolution:</b> Job parameters are referenced in filters and added columns using
    /// placeholder syntax like <c>"{CustomerId}"</c>. The system resolves these at runtime from the
    /// <see cref="Parameters"/> dictionary.
    /// </para>
    /// <para>
    /// <b>Context Injection:</b> When the source connection type is Application and target is Reporting,
    /// the system automatically injects a context column (typically CustomerId) using a parameter value.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// // Filtered historical sync from Production to Dev
    /// var job = new SyncJobConfig(
    ///     name: "Sync-Orders-Last7Days",
    ///     description: "Sync last 7 days of orders from Prod to Dev",
    ///     sourceConnection: "AppDB_Prod",
    ///     targetConnection: "AppDB_Dev",
    ///     parameters: new Dictionary<string, string> {
    ///         { "StartDate", "2025-11-24" },
    ///         { "EndDate", "2025-12-01" }
    ///     },
    ///     tables: new[] {
    ///         new TableTaskConfig(
    ///             source: "Orders",
    ///             target: "Orders",
    ///             enabled: true,
    ///             preSyncTargetAction: true,
    ///             filter: new FilterConfig(
    ///                 dateColumn: "OrderDate",
    ///                 startDate: "{StartDate}",
    ///                 endDate: "{EndDate}"
    ///             )
    ///         )
    ///     }
    /// );
    /// ]]>
    /// </code>
    /// </example>
    public class SyncJobConfig
    {
        private readonly Dictionary<string, string> _parameters;
        private readonly TableTaskConfig[] _tables;

        /// <summary>
        /// Gets the unique name of this sync job.
        /// </summary>
        /// <remarks>
        /// Job names should be descriptive and indicate the purpose (e.g., "Sync-Customers-ProdToDev").
        /// Job names must be unique within a configuration file.
        /// </remarks>
        public string Name { get; }

        /// <summary>
        /// Gets the optional description of this sync job.
        /// </summary>
        /// <remarks>
        /// Use descriptions to document the purpose, schedule, or special considerations for this job.
        /// </remarks>
        public string? Description { get; }

        /// <summary>
        /// Gets the name of the source connection to read data from.
        /// Must reference a connection defined in the <see cref="SyncConfiguration.Connections"/> list.
        /// </summary>
        public string SourceConnection { get; }

        /// <summary>
        /// Gets the name of the target connection to write data to.
        /// Must reference a connection defined in the <see cref="SyncConfiguration.Connections"/> list.
        /// </summary>
        public string TargetConnection { get; }

        /// <summary>
        /// Gets the parameters for this job, used for filter resolution and context injection.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Parameters are referenced in filters and added columns using placeholder syntax:
        /// </para>
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// Filter date range: <c>"{StartDate}"</c>, <c>"{EndDate}"</c>
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// Filter key: <c>"{CustomerId}"</c>
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// Added column context injection: <c>"{CustomerId}"</c>
        /// </description>
        /// </item>
        /// </list>
        /// <para>
        /// Common parameters include: CustomerId, StartDate, EndDate, LocationId, etc.
        /// </para>
        /// </remarks>
        public IReadOnlyDictionary<string, string> Parameters => _parameters;

        /// <summary>
        /// Gets the list of table tasks to execute in this job.
        /// </summary>
        /// <remarks>
        /// Each table task defines synchronization for one source→target table pair.
        /// Tables are processed sequentially in the order defined. At least one table
        /// task must be defined and enabled.
        /// </remarks>
        public IReadOnlyList<TableTaskConfig> Tables => _tables;

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncJobConfig"/> class.
        /// </summary>
        /// <param name="name">
        /// The unique name for this sync job. Must not be null or empty.
        /// </param>
        /// <param name="description">
        /// Optional description of the job's purpose or behavior.
        /// </param>
        /// <param name="sourceConnection">
        /// The name of the source connection. Must not be null or empty.
        /// Must reference a connection defined in the configuration.
        /// </param>
        /// <param name="targetConnection">
        /// The name of the target connection. Must not be null or empty.
        /// Must reference a connection defined in the configuration.
        /// </param>
        /// <param name="parameters">
        /// Dictionary of parameters used for filter resolution and context injection.
        /// Can be empty but not null.
        /// </param>
        /// <param name="tables">
        /// The list of table tasks to execute. Must contain at least one table task.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="name"/>, <paramref name="sourceConnection"/>,
        /// <paramref name="targetConnection"/>, <paramref name="parameters"/>, or
        /// <paramref name="tables"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="name"/>, <paramref name="sourceConnection"/>, or
        /// <paramref name="targetConnection"/> is empty or whitespace, or when
        /// <paramref name="tables"/> is empty.
        /// </exception>
        public SyncJobConfig(
            string name,
            string? description,
            string sourceConnection,
            string targetConnection,
            IDictionary<string, string> parameters,
            IEnumerable<TableTaskConfig> tables)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Job name cannot be empty or whitespace.", nameof(name));
            if (sourceConnection == null)
                throw new ArgumentNullException(nameof(sourceConnection));
            if (string.IsNullOrWhiteSpace(sourceConnection))
                throw new ArgumentException("Source connection name cannot be empty or whitespace.", nameof(sourceConnection));
            if (targetConnection == null)
                throw new ArgumentNullException(nameof(targetConnection));
            if (string.IsNullOrWhiteSpace(targetConnection))
                throw new ArgumentException("Target connection name cannot be empty or whitespace.", nameof(targetConnection));
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));

            _tables = tables.ToArray();
            if (_tables.Length == 0)
                throw new ArgumentException("Job must contain at least one table task.", nameof(tables));

            Name = name;
            Description = description;
            SourceConnection = sourceConnection;
            TargetConnection = targetConnection;
            _parameters = new Dictionary<string, string>(parameters);
        }
    }
}
