// ============================================================================
// File: SchemaMismatchBehavior.cs
// Author: Gary Wu
// Date: 2025-12-01
// Project: ReportSyncer
// Description: Enumeration defining schema mismatch handling strategies.
// ============================================================================

namespace ReportSyncer.Core.Configuration
{
    /// <summary>
    /// Defines how the system should respond when schema mismatches are detected
    /// between source and target tables during pre-flight validation.
    /// </summary>
    /// <remarks>
    /// Schema mismatches include situations such as missing columns, type incompatibilities,
    /// or other structural differences that could prevent successful data synchronization.
    /// This policy is enforced during the pre-flight validation phase before any data
    /// modification occurs.
    /// </remarks>
    public enum SchemaMismatchBehavior
    {
        /// <summary>
        /// Fail the synchronization operation immediately when a schema mismatch is detected.
        /// This is the safest option and prevents potentially incorrect data transfers.
        /// </summary>
        Fail,

        /// <summary>
        /// Log a warning about the schema mismatch but continue with the synchronization.
        /// Use this option when mismatches are expected and acceptable (e.g., extra target columns).
        /// </summary>
        Warn,

        /// <summary>
        /// Ignore schema mismatches entirely and proceed with synchronization.
        /// This is the least safe option and should only be used in specialized scenarios
        /// where schema differences are well-understood and intentional.
        /// </summary>
        Ignore
    }
}
