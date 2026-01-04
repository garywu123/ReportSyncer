// ============================================================================
// File: LogFilePathProvider.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-04
// Description: Generates JSONL log file paths with timestamps.
// ============================================================================

namespace ReportSyncer.Console.Logging;

/// <summary>
/// Provides log file path generation with timestamp-based naming.
/// </summary>
/// <remarks>
/// Each process run creates a new JSONL log file with format: 
/// {directory}/{prefix}-{timestamp}.jsonl
/// </remarks>
public static class LogFilePathProvider
{
    /// <summary>
    /// Builds a JSONL log file path with timestamp.
    /// </summary>
    /// <param name="directory">Directory for the log file (relative or absolute).</param>
    /// <param name="fileNamePrefix">Prefix for the log file name.</param>
    /// <param name="now">Current timestamp for file naming.</param>
    /// <returns>Full path to the JSONL log file.</returns>
    /// <example>
    /// <code>
    /// var path = LogFilePathProvider.BuildJsonlPath("logs", "reportsyncer", DateTimeOffset.Now);
    /// // Returns: "logs/reportsyncer-20260104-143025-123.jsonl"
    /// </code>
    /// </example>
    public static string BuildJsonlPath(string directory, string fileNamePrefix, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Directory cannot be null or whitespace.", nameof(directory));
        }

        if (string.IsNullOrWhiteSpace(fileNamePrefix))
        {
            throw new ArgumentException("File name prefix cannot be null or whitespace.", nameof(fileNamePrefix));
        }

        // Normalize directory separators and remove trailing slashes
        var normalizedDirectory = directory.Replace('\\', '/').TrimEnd('/');

        // Format timestamp as yyyyMMdd-HHmmss-fff (file-system safe)
        var timestamp = now.ToString("yyyyMMdd-HHmmss-fff");

        // Build file name: {prefix}-{timestamp}.jsonl
        var fileName = $"{fileNamePrefix}-{timestamp}.jsonl";

        // Combine path
        return Path.Combine(normalizedDirectory, fileName);
    }
}
