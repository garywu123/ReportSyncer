// ============================================================================
// File: TextLogJobProgressReporter.cs
// Author: Gary Wu
// Project: ReportSyncer
// Date: 2026-01-06
// Description: Reports job/table progress as human-readable text logs to file.
// ============================================================================

using Microsoft.Extensions.Logging;
using ReportSyncer.Console.Logging;
using ReportSyncer.Core.Observability;

namespace ReportSyncer.Console.Observability;

/// <summary>
/// Reports job and table progress events as human-readable text logs.
/// </summary>
/// <remarks>
/// <para>
/// This reporter writes progress events to the file log (via ILogger) in human-readable format.
/// InProgress events are throttled to reduce file size; terminal events are always logged immediately.
/// </para>
/// <para>
/// Design:
/// - Uses ILogger for output (Serilog file sink handles the actual file writing).
/// - Throttles InProgress events using ProgressLogThrottler (default 5 seconds).
/// - Terminal events (Started/Completed/Failed/Skipped) are never throttled.
/// - Thread-safe for concurrent progress reporting.
/// </para>
/// <example>
/// <code><![CDATA[
/// var throttler = new ProgressLogThrottler(TimeProvider.System, TimeSpan.FromSeconds(5));
/// var reporter = new TextLogJobProgressReporter(logger, throttler);
/// 
/// reporter.Report(new JobProgressEvent(
///     JobId: "job1",
///     Kind: ProgressEventKind.Started,
///     Phase: ProgressPhase.Execution,
///     UtcTimestamp: DateTimeOffset.UtcNow));
/// ]]></code>
/// </example>
/// </remarks>
public sealed class TextLogJobProgressReporter : IJobProgressReporter
{
    private readonly ILogger<TextLogJobProgressReporter> _logger;
    private readonly ProgressLogThrottler _throttler;

    /// <summary>
    /// Initializes a new instance of <see cref="TextLogJobProgressReporter"/>.
    /// </summary>
    /// <param name="logger">Logger for writing progress text.</param>
    /// <param name="throttler">Throttler to control InProgress event frequency.</param>
    public TextLogJobProgressReporter(
        ILogger<TextLogJobProgressReporter> logger,
        ProgressLogThrottler throttler)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _throttler = throttler ?? throw new ArgumentNullException(nameof(throttler));
    }

    /// <summary>
    /// Reports a job progress event.
    /// </summary>
    /// <param name="evt">The job progress event.</param>
    public void Report(JobProgressEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var isTerminal = evt.Kind != ProgressEventKind.InProgress;
        var phase = evt.Phase.ToString();

        // Check throttle (terminal events always pass)
        if (!_throttler.ShouldWrite(evt.JobId, "_job_", phase, isTerminal))
        {
            return; // Throttled
        }

        // Format message based on event kind
        var message = evt.Kind switch
        {
            ProgressEventKind.Started => FormatJobStarted(evt),
            ProgressEventKind.InProgress => FormatJobInProgress(evt),
            ProgressEventKind.Completed => FormatJobCompleted(evt),
            ProgressEventKind.Failed => FormatJobFailed(evt),
            ProgressEventKind.Skipped => FormatJobSkipped(evt),
            _ => $"Job {evt.JobId} {evt.Kind} {phase}"
        };

        // Log with appropriate level
        var logLevel = evt.Kind == ProgressEventKind.Failed ? LogLevel.Error : LogLevel.Information;
        _logger.Log(logLevel, message);
    }

    /// <summary>
    /// Reports a table progress event.
    /// </summary>
    /// <param name="evt">The table progress event.</param>
    public void Report(TableProgressEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var isTerminal = evt.Kind != ProgressEventKind.InProgress;
        var table = evt.Table.ToString();
        var phase = evt.Phase.ToString();

        // Check throttle (terminal events always pass)
        if (!_throttler.ShouldWrite(evt.JobId, table, phase, isTerminal))
        {
            return; // Throttled
        }

        // Format message based on event kind
        var message = evt.Kind switch
        {
            ProgressEventKind.Started => FormatTableStarted(evt),
            ProgressEventKind.InProgress => FormatTableInProgress(evt),
            ProgressEventKind.Completed => FormatTableCompleted(evt),
            ProgressEventKind.Failed => FormatTableFailed(evt),
            ProgressEventKind.Skipped => FormatTableSkipped(evt),
            _ => $"Job {evt.JobId} Table {table} {evt.Kind} {phase}"
        };

        // Log with appropriate level
        var logLevel = evt.Kind == ProgressEventKind.Failed ? LogLevel.Error : LogLevel.Information;
        _logger.Log(logLevel, message);
    }

    // ========================================================================
    // Job Message Formatting
    // ========================================================================

    private static string FormatJobStarted(JobProgressEvent evt)
    {
        return $"Job {evt.JobId} started {evt.Phase}";
    }

    private static string FormatJobInProgress(JobProgressEvent evt)
    {
        var elapsed = evt.Elapsed?.TotalSeconds.ToString("F1") ?? "?";
        var metrics = evt.Metrics != null
            ? $" ({evt.Metrics.RowsProcessed}/{evt.Metrics.TotalRowsPlanned} rows, {evt.Metrics.PercentComplete:F1}%)"
            : "";
        return $"Job {evt.JobId} {evt.Phase} in progress: {elapsed}s elapsed{metrics}";
    }

    private static string FormatJobCompleted(JobProgressEvent evt)
    {
        var elapsed = evt.Elapsed?.TotalSeconds.ToString("F1") ?? "?";
        var metrics = evt.Metrics != null
            ? $" ({evt.Metrics.RowsProcessed} rows)"
            : "";
        return $"Job {evt.JobId} completed {evt.Phase}: {elapsed}s{metrics}";
    }

    private static string FormatJobFailed(JobProgressEvent evt)
    {
        var error = evt.ErrorMessage ?? evt.ErrorCode ?? "Unknown error";
        return $"Job {evt.JobId} failed {evt.Phase}: {error}";
    }

    private static string FormatJobSkipped(JobProgressEvent evt)
    {
        var reason = evt.Message ?? "No reason provided";
        return $"Job {evt.JobId} skipped {evt.Phase}: {reason}";
    }

    // ========================================================================
    // Table Message Formatting
    // ========================================================================

    private static string FormatTableStarted(TableProgressEvent evt)
    {
        var dryRun = evt.IsDryRun ? " (dry-run)" : "";
        return $"Job {evt.JobId} Table {evt.Table} started {evt.Phase}{dryRun}";
    }

    private static string FormatTableInProgress(TableProgressEvent evt)
    {
        var metrics = evt.Metrics;
        if (metrics != null && metrics.TotalRowsPlanned > 0)
        {
            var percent = (double)metrics.RowsProcessed / metrics.TotalRowsPlanned.Value * 100.0;
            return $"Job {evt.JobId} Table {evt.Table} {evt.Phase} progress: {metrics.RowsProcessed}/{metrics.TotalRowsPlanned} rows ({percent:F1}%)";
        }
        
        var rows = evt.RowsAffected?.ToString() ?? "?";
        return $"Job {evt.JobId} Table {evt.Table} {evt.Phase} progress: {rows} rows processed";
    }

    private static string FormatTableCompleted(TableProgressEvent evt)
    {
        var elapsed = evt.Elapsed?.TotalSeconds.ToString("F1") ?? "?";
        var rows = evt.RowsAffected?.ToString() ?? "?";
        var dryRun = evt.IsDryRun ? " (dry-run)" : "";
        return $"Job {evt.JobId} Table {evt.Table} completed {evt.Phase}: {rows} rows in {elapsed}s{dryRun}";
    }

    private static string FormatTableFailed(TableProgressEvent evt)
    {
        var error = evt.ErrorMessage ?? evt.ErrorCode ?? "Unknown error";
        return $"Job {evt.JobId} Table {evt.Table} failed {evt.Phase}: {error}";
    }

    private static string FormatTableSkipped(TableProgressEvent evt)
    {
        var reason = evt.Message ?? "No reason provided";
        return $"Job {evt.JobId} Table {evt.Table} skipped {evt.Phase}: {reason}";
    }
}
