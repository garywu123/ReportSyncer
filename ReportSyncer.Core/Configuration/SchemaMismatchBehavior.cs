// ============================================================================
// File: SchemaMismatchBehavior.cs
// Author: Gary Wu
// Date: 2025-12-01
// Project: ReportSyncer
// Description: Enumeration defining schema mismatch handling strategies.
// ============================================================================

namespace ReportSyncer.Core.Configuration;

/// <summary>
/// <summary>
/// Schema mismatch handling behaviors.
/// </summary>
public enum SchemaMismatchBehavior
{
    /// <summary>
    /// <summary>
    /// Fail the synchronization operation immediately when a schema mismatch is detected.
    /// </summary>
    Fail,

    /// <summary>
    /// <summary>
    /// Log a warning about the schema mismatch but continue with the synchronization.
    /// </summary>
    Warn,

    /// <summary>
    /// <summary>
    /// Ignore schema mismatches entirely and proceed with synchronization.
    /// </summary>
    Ignore
}