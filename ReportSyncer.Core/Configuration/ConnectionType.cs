// ============================================================================
// File: ConnectionType.cs
// Author: Gary Wu
// Date: 2025-12-01
// Project: ReportSyncer
// Description: Enumeration defining connection types for context injection rules.
// ============================================================================

namespace ReportSyncer.Core.Configuration
{
    /// <summary>
    /// Defines the type of database connection, which determines whether automatic
    /// context injection occurs during synchronization operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Connection type is critical for the context injection feature:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// When syncing from <see cref="Application"/> to <see cref="Reporting"/>, 
    /// the system automatically injects a context column (typically CustomerId) 
    /// into the target data using job parameters.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// When syncing from <see cref="Reporting"/> to <see cref="Reporting"/>, 
    /// no automatic context injection occurs; CustomerId is treated as a normal column.
    /// </description>
    /// </item>
    /// </list>
    /// </remarks>
    public enum ConnectionType
    {
        /// <summary>
        /// Application database connection. Represents operational/transactional databases
        /// that contain business application data. When syncing from Application to Reporting,
        /// context columns are automatically injected.
        /// </summary>
        Application,

        /// <summary>
        /// Reporting database connection. Represents analytical/data warehouse databases
        /// optimized for reporting and business intelligence queries. Context columns
        /// are expected to already exist in the data.
        /// </summary>
        Reporting
    }
}
