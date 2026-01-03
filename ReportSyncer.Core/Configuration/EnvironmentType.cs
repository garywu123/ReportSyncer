// ============================================================================
// File: EnvironmentType.cs
// Author: Gary Wu
// Date: 2025-12-01
// Project: ReportSyncer
// Description: Enumeration defining environment types for database connections.
// ============================================================================

namespace ReportSyncer.Core.Configuration;

/// <summary>
/// Environment types (Dev, Prod, etc.).
/// </summary>
/// <summary>
/// Environment types (Dev, Prod, etc.).
/// </summary>
public enum EnvironmentType
{
    /// <summary>
    /// Production environment.
    /// </summary>
    Prod,

    /// <summary>
    /// Development environment.
    /// </summary>
    Dev,

    /// <summary>
    /// Test/QA environment.
    /// </summary>
    Test,

    /// <summary>
    /// Staging environment.
    /// </summary>
    Staging,

    /// <summary>
    /// Reporting environment.
    /// </summary>
    Reporting
}