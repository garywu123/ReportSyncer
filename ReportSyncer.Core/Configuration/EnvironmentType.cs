// ============================================================================
// File: EnvironmentType.cs
// Author: Gary Wu
// Date: 2025-12-01
// Project: ReportSyncer
// Description: Enumeration defining environment types for database connections.
// ============================================================================

namespace ReportSyncer.Core.Configuration
{
    /// <summary>
    /// Defines the environment type for a database connection, used to enforce
    /// safety rules such as preventing Prod-to-Prod synchronization.
    /// </summary>
    /// <remarks>
    /// The environment type is used in conjunction with <see cref="SafetyConfig"/> 
    /// to prevent dangerous sync operations like Production-to-Production copies 
    /// when <c>ForbidProdToProd</c> is enabled.
    /// </remarks>
    public enum EnvironmentType
    {
        /// <summary>
        /// Production environment. Syncs from or to Production are subject to 
        /// strict safety rules to prevent accidental data loss or corruption.
        /// </summary>
        Prod,

        /// <summary>
        /// Development environment. Typically used as a target for data refreshes 
        /// from Production or Test environments.
        /// </summary>
        Dev,

        /// <summary>
        /// Test/QA environment. Used for testing and validation before deploying 
        /// changes to Production.
        /// </summary>
        Test,

        /// <summary>
        /// Staging environment. Pre-production environment that mirrors Production 
        /// configuration for final testing.
        /// </summary>
        Staging,

        /// <summary>
        /// Reporting environment. Specialized environment for business intelligence 
        /// and reporting workloads.
        /// </summary>
        Reporting
    }
}
