// ============================================================================
// File: IRunLogWriter.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: January 1, 2026
// Description: Abstraction for writing structured run log entries to persistent storage.
//              Implementations handle directory creation, file naming, and jsonl formatting.
// ============================================================================

namespace ReportSyncer.Core.Observability.RunLog;

/// <summary>
/// Abstraction for writing structured run log entries.
/// Implementations are responsible for persistence details (file system, database, etc.).
/// </summary>
/// <remarks>
/// The writer handles:
/// - Directory and file creation
/// - Run ID generation and management
/// - Thread-safe appending of jsonl entries
/// - Correlation between events in the same run
/// </remarks>
public interface IRunLogWriter
{
    /// <summary>
    /// Appends a job-level log entry.
    /// </summary>
    /// <param name="entry">The log entry to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AppendJobAsync(RunLogEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends a table-level log entry.
    /// </summary>
    /// <param name="entry">The log entry to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AppendTableAsync(RunLogEntry entry, CancellationToken cancellationToken = default);
}
