// ============================================================================
// File: SyncJobConfig.cs
// Author: Gary Wu
// Date: 2025-12-01
// Project: ReportSyncer
// Description: Configuration for a synchronization job containing multiple table tasks.
// ============================================================================

namespace ReportSyncer.Core.Configuration
{
    /// <summary>
    /// Configuration for a sync job.
    /// </summary>
    public class SyncJobConfig
    {
        private readonly Dictionary<string, string> _parameters;
        private readonly TableTaskConfig[] _tables;

        /// <summary>
        /// Gets the unique name of this sync job.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the optional description of this sync job.
        /// </summary>
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
        public IReadOnlyDictionary<string, string> Parameters => _parameters;

        /// <summary>
        /// Gets the list of table tasks to execute in this job.
        /// </summary>
        public IReadOnlyList<TableTaskConfig> Tables => _tables;

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncJobConfig"/> class.
        /// </summary>
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
            ArgumentNullException.ThrowIfNull(name);
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Job name cannot be empty or whitespace.", nameof(name));
            ArgumentNullException.ThrowIfNull(sourceConnection);
            if (string.IsNullOrWhiteSpace(sourceConnection))
                throw new ArgumentException("Source connection name cannot be empty or whitespace.", nameof(sourceConnection));
            ArgumentNullException.ThrowIfNull(targetConnection);
            if (string.IsNullOrWhiteSpace(targetConnection))
                throw new ArgumentException("Target connection name cannot be empty or whitespace.", nameof(targetConnection));
            ArgumentNullException.ThrowIfNull(parameters);
            ArgumentNullException.ThrowIfNull(tables);

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
