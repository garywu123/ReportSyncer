// ============================================================================
// File: LogRetentionCleaner.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-04
// Description: Best-effort cleanup of old log files based on retention policy.
// ============================================================================

namespace ReportSyncer.Console.Logging;

/// <summary>
/// Cleans up old log files, retaining only the most recent N files.
/// </summary>
/// <remarks>
/// This is a best-effort operation: failures during cleanup do not throw exceptions.
/// Warnings should be logged by the caller if cleanup fails.
/// </remarks>
public static class LogRetentionCleaner
{
    /// <summary>
    /// Attempts to clean up old log files, keeping only the most recent N files.
    /// </summary>
    /// <param name="directory">Directory containing log files.</param>
    /// <param name="fileNamePrefix">Prefix for log files to consider for cleanup.</param>
    /// <param name="retentionCount">Number of recent log files to keep.</param>
    /// <returns>
    /// A tuple containing:
    /// - success: true if cleanup completed without errors, false otherwise
    /// - deletedCount: number of files successfully deleted
    /// - errorMessage: error message if success is false, otherwise null
    /// </returns>
    /// <example>
    /// <code>
    /// var (success, deleted, error) = LogRetentionCleaner.BestEffortCleanup("logs", "reportsyncer", 20);
    /// if (!success)
    /// {
    ///     logger.Warning($"Log retention cleanup warning: {error}");
    /// }
    /// </code>
    /// </example>
    public static (bool success, int deletedCount, string? errorMessage) BestEffortCleanup(
        string directory, 
        string fileNamePrefix, 
        int retentionCount)
    {
        if (retentionCount <= 0)
        {
            return (false, 0, "Retention count must be positive.");
        }

        try
        {
            // Check if directory exists
            if (!Directory.Exists(directory))
            {
                // Not an error - directory may not exist on first run
                return (true, 0, null);
            }

            // Find all matching log files
            var pattern = $"{fileNamePrefix}-*.jsonl";
            var files = Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly);

            if (files.Length <= retentionCount)
            {
                // Nothing to delete
                return (true, 0, null);
            }

            // Sort by last write time (newest first)
            var sortedFiles = files
                .Select(f => new FileInfo(f))
                .OrderByDescending(fi => fi.LastWriteTimeUtc)
                .ToArray();

            // Delete files beyond retention count
            var filesToDelete = sortedFiles.Skip(retentionCount).ToArray();
            var deletedCount = 0;
            var errors = new List<string>();

            foreach (var file in filesToDelete)
            {
                try
                {
                    file.Delete();
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"{file.Name}: {ex.Message}");
                }
            }

            if (errors.Count > 0)
            {
                var errorMessage = $"Failed to delete {errors.Count} file(s): {string.Join("; ", errors)}";
                return (false, deletedCount, errorMessage);
            }

            return (true, deletedCount, null);
        }
        catch (Exception ex)
        {
            return (false, 0, $"Cleanup failed: {ex.Message}");
        }
    }
}
