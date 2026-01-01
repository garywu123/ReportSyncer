// ============================================================================
// File: RunLogJobProgressReporter.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: January 1, 2026
// Description: IJobProgressReporter implementation that writes structured log
//              entries to IRunLogWriter. Maps progress events to RunLogEntry
//              and enforces correlation contract.
// ============================================================================

namespace ReportSyncer.Core.Observability.RunLog;

/// <summary>
/// Progress reporter that writes structured log entries via <see cref="IRunLogWriter"/>.
/// Enforces correlation contract and maps event kinds to log levels.
/// </summary>
/// <remarks>
/// Event kind to log level mapping:
/// - Started, Completed → Info
/// - InProgress → Debug
/// - Skipped → Warn
/// - Failed → Error
/// 
/// Can optionally wrap another <see cref="IJobProgressReporter"/> to enable
/// simultaneous reporting to multiple destinations (e.g., console + run log).
/// </remarks>
public sealed class RunLogJobProgressReporter : IJobProgressReporter
{
    private readonly IRunLogWriter _writer;
    private readonly IJobProgressReporter? _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunLogJobProgressReporter"/> class.
    /// </summary>
    /// <param name="writer">The run log writer.</param>
    /// <param name="inner">Optional inner reporter for chaining (e.g., console logging).</param>
    public RunLogJobProgressReporter(IRunLogWriter writer, IJobProgressReporter? inner = null)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _inner = inner;
    }

    /// <inheritdoc/>
    public void Report(JobProgressEvent evt)
    {
        // Enforce correlation contract
        CorrelationLogContract.ValidateJob(evt.JobId, evt.Phase.ToString());

        // Build log entry
        var entry = new RunLogEntry
        {
            UtcTimestamp = evt.UtcTimestamp.ToString("O"),
            Level = MapEventKindToLevel(evt.Kind),
            JobId = evt.JobId,
            EventKind = evt.Kind.ToString(),
            Phase = evt.Phase.ToString(),
            Table = null,
            ElapsedMs = evt.Elapsed.HasValue ? (long)evt.Elapsed.Value.TotalMilliseconds : null,
            RowsAffected = null,
            IsDryRun = false,
            Metrics = MapMetrics(evt.Metrics),
            Message = evt.Message,
            ErrorCode = evt.ErrorCode,
            ErrorMessage = evt.ErrorMessage
        };

        // Write asynchronously (fire-and-forget for now to avoid blocking execution)
        // In production, consider using a background queue or proper async coordination
        _ = _writer.AppendJobAsync(entry);

        // Chain to inner reporter if present
        _inner?.Report(evt);
    }

    /// <inheritdoc/>
    public void Report(TableProgressEvent evt)
    {
        // Enforce correlation contract
        CorrelationLogContract.ValidateTable(evt.JobId, evt.Table, evt.Phase);

        // Build log entry
        var entry = new RunLogEntry
        {
            UtcTimestamp = evt.UtcTimestamp.ToString("O"),
            Level = MapEventKindToLevel(evt.Kind),
            JobId = evt.JobId,
            EventKind = evt.Kind.ToString(),
            Phase = evt.Phase.ToString(),
            Table = evt.Table.ToString(),
            ElapsedMs = evt.Elapsed.HasValue ? (long)evt.Elapsed.Value.TotalMilliseconds : null,
            RowsAffected = evt.RowsAffected,
            IsDryRun = evt.IsDryRun,
            Metrics = MapMetrics(evt.Metrics),
            Message = evt.Message,
            ErrorCode = evt.ErrorCode,
            ErrorMessage = evt.ErrorMessage
        };

        // Write asynchronously
        _ = _writer.AppendTableAsync(entry);

        // Chain to inner reporter if present
        _inner?.Report(evt);
    }

    /// <summary>
    /// Maps <see cref="ProgressEventKind"/> to log level string.
    /// </summary>
    private static string MapEventKindToLevel(ProgressEventKind kind) => kind switch
    {
        ProgressEventKind.Started => "Info",
        ProgressEventKind.Completed => "Info",
        ProgressEventKind.InProgress => "Debug",
        ProgressEventKind.Skipped => "Warn",
        ProgressEventKind.Failed => "Error",
        _ => "Info"
    };

    /// <summary>
    /// Maps <see cref="ProgressMetrics"/> to <see cref="RunLogEntry.MetricsData"/>.
    /// </summary>
    private static RunLogEntry.MetricsData? MapMetrics(ProgressMetrics? metrics)
    {
        if (metrics is null)
            return null;

        return new RunLogEntry.MetricsData
        {
            RowsProcessed = metrics.RowsProcessed,
            TotalRowsPlanned = metrics.TotalRowsPlanned,
            PercentComplete = metrics.PercentComplete,
            ThroughputRowsPerSec = metrics.ThroughputRowsPerSec,
            EtaSeconds = metrics.EtaSeconds
        };
    }
}
