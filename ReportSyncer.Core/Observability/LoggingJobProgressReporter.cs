// ============================================================================
// File: LoggingJobProgressReporter.cs
// Author: Codex
// Project: ReportSyncer
// Description: Emits progress events via the logging pipeline.
// ============================================================================

using DotNetToolkit.Logging;

namespace ReportSyncer.Core.Observability;

public sealed class LoggingJobProgressReporter : IJobProgressReporter
{
    private readonly ILogService _log;

    public LoggingJobProgressReporter(ILogService log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public void Report(JobProgressEvent evt)
    {
        if (evt is null) return;
        var message = $"Job {evt.JobId} {evt.Kind} ({evt.Phase})";
        switch (evt.Kind)
        {
            case ProgressEventKind.Started:
            case ProgressEventKind.Completed:
                _log.LogInformation(message);
                break;
            case ProgressEventKind.InProgress:
                _log.LogDebug(message);
                break;
            case ProgressEventKind.Skipped:
                _log.LogWarning(message);
                break;
            case ProgressEventKind.Failed:
                _log.LogError($"{message}: {evt.ErrorMessage ?? evt.Message}");
                break;
            default:
                _log.LogWarning($"Unknown ProgressEventKind: {evt.Kind}");
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
                _log.LogInformation(message);
                break;
            case ProgressEventKind.InProgress:
                _log.LogDebug($"{message} - Progress: {evt.Metrics?.RowsProcessed ?? 0}/{evt.Metrics?.TotalRowsPlanned ?? 0}");
                break;
            case ProgressEventKind.Skipped:
                _log.LogWarning(message);
                break;
            case ProgressEventKind.Failed:
                _log.LogError($"{message}: {evt.ErrorMessage ?? evt.Message}");
                break;
            default:
                _log.LogWarning($"Unknown ProgressEventKind: {evt.Kind}");
                break;
        }
    }
}
