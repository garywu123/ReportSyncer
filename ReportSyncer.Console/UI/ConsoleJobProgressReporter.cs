// ============================================================================
// File: ConsoleJobProgressReporter.cs
// Author: Gary Wu
// Project: ReportSyncer.Console
// Date: January 5, 2026
// Description: IJobProgressReporter adapter that enqueues events to UiEventQueue.
// ============================================================================

using DotNetToolkit.Logging;
using ReportSyncer.Core.Observability;

namespace ReportSyncer.Console.UI;

/// <summary>
/// Adapter that converts Core progress events into UI events and enqueues them.
/// This implementation never renders to console or throws exceptions.
/// </summary>
/// <remarks>
/// Rule: Reporter must NOT render or call any Console APIs.
/// Rule: On invalid payload, log warning and return (never throw).
/// </remarks>
public sealed class ConsoleJobProgressReporter : IJobProgressReporter
{
    private readonly UiEventQueue _queue;
    private readonly ILogService _log;

    /// <summary>
    /// Initializes a new instance of <see cref="ConsoleJobProgressReporter"/>.
    /// </summary>
    /// <param name="queue">Target queue for UI events.</param>
    /// <param name="log">Log service for warnings.</param>
    public ConsoleJobProgressReporter(UiEventQueue queue, ILogService log)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>
    /// Reports a job-level progress event.
    /// </summary>
    /// <param name="evt">Job progress event from Core.</param>
    public void Report(JobProgressEvent evt)
    {
        if (evt is null)
        {
            _log.LogWarning("ConsoleJobProgressReporter received null JobProgressEvent");
            return;
        }

        if (string.IsNullOrWhiteSpace(evt.JobId))
        {
            _log.LogWarning("ConsoleJobProgressReporter received JobProgressEvent with empty JobId");
            return;
        }

        var uiEvent = new UiEvent(
            UiEventKind.Job,
            evt,
            DateTimeOffset.UtcNow);

        if (!_queue.TryEnqueue(uiEvent))
        {
            _log.LogWarning($"Failed to enqueue JobProgressEvent for JobId={evt.JobId}");
        }
    }

    /// <summary>
    /// Reports a table-level progress event.
    /// </summary>
    /// <param name="evt">Table progress event from Core.</param>
    public void Report(TableProgressEvent evt)
    {
        if (evt is null)
        {
            _log.LogWarning("ConsoleJobProgressReporter received null TableProgressEvent");
            return;
        }

        if (string.IsNullOrWhiteSpace(evt.JobId))
        {
            _log.LogWarning("ConsoleJobProgressReporter received TableProgressEvent with empty JobId");
            return;
        }

        if (evt.Table is null)
        {
            _log.LogWarning("ConsoleJobProgressReporter received TableProgressEvent with null Table");
            return;
        }

        var uiEvent = new UiEvent(
            UiEventKind.Table,
            evt,
            DateTimeOffset.UtcNow);

        if (!_queue.TryEnqueue(uiEvent))
        {
            _log.LogWarning($"Failed to enqueue TableProgressEvent for Table={evt.Table}");
        }
    }
}
