// ============================================================================
// File: RunLogLayoutSpec.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: January 1, 2026
// Description: Defines directory structure and file naming conventions for
//              structured run logs. Provides path sanitization to ensure
//              cross-platform compatibility.
// ============================================================================

using ReportSyncer.Core.Schema;

namespace ReportSyncer.Core.Observability.RunLog;

/// <summary>
/// Specifies the layout of run logs on the file system.
/// Defines directory structure and file naming conventions for job and table logs.
/// </summary>
/// <param name="BaseDirectory">Root directory for all run logs.</param>
/// <remarks>
/// Directory structure:
/// <code>
/// {BaseDirectory}/
///   {JobId}/
///     {runId}/
///       job.jsonl               # Job-level events
///       tables/
///         {Table}.jsonl         # Table-level events
/// </code>
/// All identifiers are sanitized to ensure cross-platform path compatibility.
/// </remarks>
public sealed record RunLogLayoutSpec(string BaseDirectory)
{
    /// <summary>
    /// Gets the run directory for a specific job and run instance.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="runId">The run instance identifier (e.g., timestamp + guid).</param>
    /// <returns>Absolute path to the run directory.</returns>
    public string GetRunDirectory(string jobId, string runId)
        => Path.Combine(BaseDirectory, Sanitize(jobId), runId);

    /// <summary>
    /// Gets the path for the job-level log file.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="runId">The run instance identifier.</param>
    /// <returns>Absolute path to job.jsonl file.</returns>
    public string GetJobLogPath(string jobId, string runId)
        => Path.Combine(GetRunDirectory(jobId, runId), "job.jsonl");

    /// <summary>
    /// Gets the path for a table-specific log file.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="runId">The run instance identifier.</param>
    /// <param name="table">The table identifier.</param>
    /// <returns>Absolute path to the table's jsonl file within the tables subdirectory.</returns>
    public string GetTableLogPath(string jobId, string runId, TableIdentifier table)
        => Path.Combine(GetRunDirectory(jobId, runId), "tables", $"{Sanitize(table.ToString())}.jsonl");

    /// <summary>
    /// Sanitizes a string for safe use in file paths.
    /// Replaces characters that are not letters, digits, dots, underscores, or hyphens with underscores.
    /// </summary>
    /// <param name="raw">The raw string to sanitize.</param>
    /// <returns>A sanitized string safe for use in file paths.</returns>
    /// <remarks>
    /// This ensures cross-platform compatibility by removing characters that may be
    /// invalid on some file systems (e.g., slashes, spaces, special characters).
    /// </remarks>
    public static string Sanitize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "_";

        var chars = raw.Select(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-' ? c : '_').ToArray();
        return new string(chars);
    }
}
