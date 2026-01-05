// ============================================================================
// File: LoggingJobProgressReporter.cs
// Author: Codex
// Project: ReportSyncer
// Description: Emits progress events via the logging pipeline.
// ============================================================================

using Microsoft.Extensions.Logging;

namespace ReportSyncer.Core.Observability;

public sealed class LoggingJobProgressReporter : IJobProgressReporter
{
    private readonly ILogger<LoggingJobProgressReporter> _logger;

    public LoggingJobProgressReporter(ILogger<LoggingJobProgressReporter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Report(JobProgressEvent evt)
    {
        if (evt is null) return;
        var message = $"Job {evt.JobId} {evt.Kind} ({evt.Phase})";
        switch (evt.Kind)
        {
            case ProgressEventKind.Started:
            case ProgressEventKind.Completed:
                _logger.LogInformation("{Message}", message);
                break;
            case ProgressEventKind.InProgress:
                _logger.LogDebug("{Message}", message);
                break;
            case ProgressEventKind.Skipped:
                _logger.LogWarning("{Message}", message);
                break;
            case ProgressEventKind.Failed:
                _logger.LogError("{Message}: {Error}", message, evt.ErrorMessage ?? evt.Message);
                break;
            default:
                _logger.LogWarning("Unknown ProgressEventKind: {Kind}", evt.Kind);
                break;
        }
    }

    public void Report(TableProgressEvent evt)
    {
        if (evt is null) return;
        var message = $"Job {evt.JobId} Table {evt.Table} {evt.Kind} ({evt.Phase})";
        switch (evt.Kind)
        {
            case ProgressEventKind.Started:
            case ProgressEventKind.Completed:
                _logger.LogInformation("{Message}", message);
                break;
            case ProgressEventKind.InProgress:
                _logger.LogDebug("{Message} - Progress: {Processed}/{Total}", message, evt.Metrics?.RowsProcessed ?? 0, evt.Metrics?.TotalRowsPlanned ?? 0);
                break;
            case ProgressEventKind.Skipped:
                _logger.LogWarning("{Message}", message);
                break;
            case ProgressEventKind.Failed:
                _logger.LogError("{Message}: {Error}", message, evt.ErrorMessage ?? evt.Message);
                break;
            default:
                _logger.LogWarning("Unknown ProgressEventKind: {Kind}", evt.Kind);
                break;
        }
    }
}
