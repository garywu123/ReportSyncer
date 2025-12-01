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
        /// <summary>
        /// Supported connection types.
        /// </summary>
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
