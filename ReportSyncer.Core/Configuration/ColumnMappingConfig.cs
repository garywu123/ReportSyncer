// ============================================================================
// File: ColumnMappingConfig.cs
// Author: Gary Wu
// Date: 2025-12-01
// Project: ReportSyncer
// Description: Configuration for column mapping between source and target tables.
// ============================================================================

using System.Collections.Generic;

namespace ReportSyncer.Core.Configuration
{
    /// <summary>
    /// Defines how columns are mapped between source and target tables during synchronization,
    /// including automatic mapping by name, explicit column-to-column mappings, and added columns
    /// for context injection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Column mapping controls which source columns are transferred to which target columns.
    /// The system supports three mapping strategies that can be combined:
    /// </para>
    /// <list type="number">
    /// <item>
    /// <description>
    /// <b>Automatic mapping by name:</b> When <see cref="AutomapByName"/> is true, columns
    /// with identical names in source and target are automatically mapped. This is the most
    /// common scenario.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>Explicit mappings:</b> Override automatic mapping or map columns with different names
    /// using <see cref="ExplicitMappings"/> (source column → target column dictionary).
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>Added columns:</b> Inject columns that don't exist in source or need constant/parameter
    /// values using <see cref="AddedColumns"/>. Primarily used for context injection.
    /// </description>
    /// </item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <![CDATA[
    /// // Automatic mapping only (most common)
    /// var autoMap = new ColumnMappingConfig(
    ///     automapByName: true,
    ///     explicitMappings: null,
    ///     addedColumns: null
    /// );
    /// 
    /// // Automatic + explicit mappings (rename a column)
    /// var withRename = new ColumnMappingConfig(
    ///     automapByName: true,
    ///     explicitMappings: new Dictionary<string, string> {
    ///         { "OrderID", "Order_ID" },  // Source.OrderID -> Target.Order_ID
    ///         { "CustID", "CustomerID" }  // Source.CustID -> Target.CustomerID
    ///     },
    ///     addedColumns: null
    /// );
    /// 
    /// // Automatic + context injection
    /// var withContext = new ColumnMappingConfig(
    ///     automapByName: true,
    ///     explicitMappings: null,
    ///     addedColumns: new[] {
    ///         new AddedColumnMappingConfig("CustomerId", "{CustomerId}")
    ///     }
    /// );
    /// ]]>
    /// </example>
    public class ColumnMappingConfig
    {
        private readonly Dictionary<string, string>? _explicitMappings;
        private readonly AddedColumnMappingConfig[]? _addedColumns;

        /// <summary>
        /// Gets a value indicating whether columns should be automatically mapped by name.
        /// When true, any column with the same name in source and target is automatically mapped.
        /// </summary>
        /// <remarks>
        /// Defaults to true in most scenarios. Set to false if you want complete manual control
        /// over all column mappings via <see cref="ExplicitMappings"/>.
        /// </remarks>
        public bool AutomapByName { get; }

        /// <summary>
        /// Gets the explicit column mappings (source column name → target column name).
        /// Null when no explicit mappings are defined.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Explicit mappings override automatic mappings and are used when:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Source and target columns have different names</description></item>
        /// <item><description>You need to map a subset of columns selectively</description></item>
        /// <item><description>You want to skip certain columns that would auto-map</description></item>
        /// </list>
        /// </remarks>
        public IReadOnlyDictionary<string, string>? ExplicitMappings => _explicitMappings;

        /// <summary>
        /// Gets the list of columns to add to the target insert with constant or parameter-based values.
        /// Null when no added columns are defined.
        /// </summary>
        /// <remarks>
        /// Added columns are typically used for context injection (e.g., CustomerId) when syncing
        /// from Application to Reporting databases. The values can reference job parameters using
        /// placeholder syntax like "{CustomerId}".
        /// </remarks>
        public IReadOnlyList<AddedColumnMappingConfig>? AddedColumns => _addedColumns;

        /// <summary>
        /// Initializes a new instance of the <see cref="ColumnMappingConfig"/> class.
        /// </summary>
        /// <param name="automapByName">
        /// Whether to automatically map columns with matching names between source and target.
        /// Defaults to true.
        /// </param>
        /// <param name="explicitMappings">
        /// Optional dictionary of explicit column mappings (source → target).
        /// Use this to rename columns or override automatic mappings.
        /// </param>
        /// <param name="addedColumns">
        /// Optional list of columns to add to target inserts with constant or parameter values.
        /// Use this for context injection or adding constant values.
        /// </param>
        public ColumnMappingConfig(
            bool automapByName = true,
            IDictionary<string, string>? explicitMappings = null,
            IEnumerable<AddedColumnMappingConfig>? addedColumns = null)
        {
            AutomapByName = automapByName;
            _explicitMappings = explicitMappings != null ? new Dictionary<string, string>(explicitMappings) : null;
            _addedColumns = addedColumns != null ? System.Linq.Enumerable.ToArray(addedColumns) : null;
        }
    }
}
