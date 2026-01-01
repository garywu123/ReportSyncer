// ============================================================================
// File: CorrelationLogContract.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: January 1, 2026
// Description: Defines correlation requirements for structured logging.
//              Enforces that all run log entries include necessary correlation
//              dimensions (JobId, Phase, TableIdentifier).
// ============================================================================

using ReportSyncer.Core.Schema;
using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Core.Observability.RunLog;

/// <summary>
/// Defines the correlation contract for structured logging.
/// All structured log entries must satisfy these correlation requirements
/// to enable traceability and history reconstruction.
/// </summary>
/// <remarks>
/// This contract ensures that:
/// - Job-level logs include JobId and Phase
/// - Table-level logs include JobId, TableIdentifier, and SyncPhase
/// Violation of these requirements throws InvalidOperationException to fail-fast.
/// </remarks>
public static class CorrelationLogContract
{
    /// <summary>
    /// Validates that a job-level event includes required correlation dimensions.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="phase">The job phase (e.g., Preflight, Execution).</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when JobId or Phase is missing or empty.
    /// </exception>
    public static void ValidateJob(string jobId, string phase)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new InvalidOperationException("Correlation requires JobId.");
        
        if (string.IsNullOrWhiteSpace(phase))
            throw new InvalidOperationException("Correlation requires Phase.");
    }

    /// <summary>
    /// Validates that a table-level event includes required correlation dimensions.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="table">The table identifier.</param>
    /// <param name="phase">The sync phase (e.g., Preflight, Delete, Insert).</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when JobId is missing, empty, or TableIdentifier is null.
    /// </exception>
    public static void ValidateTable(string jobId, TableIdentifier table, SyncPhase phase)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new InvalidOperationException("Correlation requires JobId.");
        
        if (table is null)
            throw new InvalidOperationException("Correlation requires TableIdentifier.");
        
        // SyncPhase is an enum, always provided - no additional validation needed
    }
}
