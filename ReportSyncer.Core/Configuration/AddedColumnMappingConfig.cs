// ============================================================================
// File: AddedColumnMappingConfig.cs
// Author: Gary Wu
// Date: 2025-12-01
// Project: ReportSyncer
// Description: Configuration for injected/added columns in target inserts.
// ============================================================================

using System;

namespace ReportSyncer.Core.Configuration
{
    /// <summary>
    /// Defines a column to be added to target insert operations with a constant value
    /// or parameter-based value. Primarily used for context injection scenarios.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Added columns are columns that exist in the target table but not in the source table,
    /// or that need to be populated with a specific value regardless of source data.
    /// </para>
    /// <para>
    /// <b>Context Injection:</b> When syncing from Application to Reporting databases,
    /// added columns are typically used to inject context identifiers like CustomerId.
    /// The value can reference job parameters using the <c>"{ParameterName}"</c> placeholder syntax.
    /// </para>
    /// <para>
    /// <b>Constant Values:</b> Added columns can also be used to inject constant values
    /// (e.g., a fixed status code or processing timestamp).
    /// </para>
    /// </remarks>
    /// <example>
    /// <code><![CDATA[
    /// // Context injection: inject CustomerId from job parameters
    /// var contextColumn = new AddedColumnMappingConfig(
    ///     columnName: "CustomerId",
    ///     value: "{CustomerId}"  // Resolved from job.Parameters["CustomerId"]
    /// );
    /// 
    /// // Constant value injection
    /// var constantColumn = new AddedColumnMappingConfig(
    ///     columnName: "DataSource",
    ///     value: "ProductionSync"
    /// );
    /// ]]></code>
    /// </example>
    public class AddedColumnMappingConfig
    {
        /// <summary>
        /// Gets the name of the column to add to the target insert operations.
        /// This column must exist in the target table schema.
        /// </summary>
        public string ColumnName { get; }

        /// <summary>
        /// Gets the value to insert into the added column.
        /// Can be a constant value or a parameter placeholder like "{CustomerId}".
        /// </summary>
        /// <remarks>
        /// Parameter placeholders are resolved from the job's Parameters dictionary at runtime.
        /// For example, "{CustomerId}" would be replaced with job.Parameters["CustomerId"].
        /// </remarks>
        public string Value { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AddedColumnMappingConfig"/> class.
        /// </summary>
        /// <param name="columnName">
        /// The name of the column to add. Must not be null or empty.
        /// </param>
        /// <param name="value">
        /// The value to insert into the column. Can be a constant or parameter placeholder.
        /// Must not be null.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="columnName"/> or <paramref name="value"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="columnName"/> is empty or whitespace.
        /// </exception>
        public AddedColumnMappingConfig(string columnName, string value)
        {
            ArgumentNullException.ThrowIfNull(columnName);
            if (string.IsNullOrWhiteSpace(columnName))
                throw new ArgumentException("Column name cannot be empty or whitespace.", nameof(columnName));
            ArgumentNullException.ThrowIfNull(value);

            ColumnName = columnName;
            Value = value;
        }
    }
}
