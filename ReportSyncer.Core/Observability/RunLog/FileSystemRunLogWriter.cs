// ============================================================================
// File: FileSystemRunLogWriter.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: January 1, 2026
// Description: File system implementation of IRunLogWriter that appends
//              structured log entries to jsonl files. Handles run ID generation,
//              directory creation, and thread-safe file writing.
// ============================================================================

using System.Collections.Concurrent;
using System.Text.Json;

namespace ReportSyncer.Core.Observability.RunLog;

/// <summary>
/// File system implementation of <see cref="IRunLogWriter"/>.
/// Writes structured log entries to jsonl (JSON Lines) files with thread-safe appending.
/// </summary>
/// <remarks>
/// Features:
/// - Generates unique run IDs for each job (timestamp + short GUID)
/// - Creates directory structure on demand
/// - Thread-safe file appending using per-file semaphores
/// - Supports both job-level and table-level log files
/// 
/// Limitations:
/// - Run IDs are generated once per job in a single process lifetime
/// - Does not support cross-process continuation of the same run
/// - Not optimized for extremely high-throughput scenarios (uses synchronous file I/O with locking)
/// </remarks>
public sealed class FileSystemRunLogWriter : IRunLogWriter
{
    private readonly RunLogLayoutSpec _layout;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ConcurrentDictionary<string, string> _jobRunIds;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemRunLogWriter"/> class.
    /// </summary>
    /// <param name="layout">The layout specification for run log directories and files.</param>
    public FileSystemRunLogWriter(RunLogLayoutSpec layout)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        _jobRunIds = new ConcurrentDictionary<string, string>();
        _fileLocks = new ConcurrentDictionary<string, SemaphoreSlim>();
    }

    /// <inheritdoc/>
    public async Task AppendJobAsync(RunLogEntry entry, CancellationToken cancellationToken = default)
    {
        var runId = GetOrCreateRunId(entry.JobId);
        var filePath = _layout.GetJobLogPath(entry.JobId, runId);
        await AppendEntryAsync(filePath, entry, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task AppendTableAsync(RunLogEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry.Table is null)
            throw new InvalidOperationException("Table-level entry must include Table identifier.");

        var runId = GetOrCreateRunId(entry.JobId);
        var tableIdentifier = Schema.TableIdentifier.Parse(entry.Table);
        var filePath = _layout.GetTableLogPath(entry.JobId, runId, tableIdentifier);
        await AppendEntryAsync(filePath, entry, cancellationToken);
    }

    /// <summary>
    /// Gets or creates a unique run ID for the specified job.
    /// The run ID is generated once per job and cached for the lifetime of this writer instance.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <returns>A unique run ID (timestamp + short GUID).</returns>
    private string GetOrCreateRunId(string jobId)
    {
        return _jobRunIds.GetOrAdd(jobId, _ =>
        {
            var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss");
            var shortGuid = Guid.NewGuid().ToString("N")[..8];
            return $"{timestamp}_{shortGuid}";
        });
    }

    /// <summary>
    /// Appends a log entry to the specified file path with thread-safe locking.
    /// Creates directories and initializes file if it doesn't exist.
    /// </summary>
    /// <param name="filePath">The target file path.</param>
    /// <param name="entry">The log entry to append.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task AppendEntryAsync(string filePath, RunLogEntry entry, CancellationToken cancellationToken)
    {
        // Ensure directory exists
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Get or create a semaphore for this file to ensure thread-safe writes
        var semaphore = _fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync(cancellationToken);
        try
        {
            // Serialize entry to a single line of JSON
            var json = JsonSerializer.Serialize(entry, _jsonOptions);
            
            // Append to file with newline
            await File.AppendAllTextAsync(filePath, json + Environment.NewLine, cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
